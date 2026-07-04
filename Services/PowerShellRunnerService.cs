using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace CoreIsolator.Services;

public interface IPowerShellRunnerService
{
    IAsyncEnumerable<string> RunScriptAsync(string scriptPath, CancellationToken cancellationToken = default);
    IAsyncEnumerable<string> RunEmbeddedScriptAsync(string resourceName, CancellationToken cancellationToken = default);
}

public class PowerShellRunnerService : IPowerShellRunnerService
{
    public async IAsyncEnumerable<string> RunEmbeddedScriptAsync(string resourceName, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
            throw new FileNotFoundException($"O script embutido '{resourceName}' não foi encontrado.");

        string tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.ps1");
        
        using (var fileStream = File.Create(tempFile))
        {
            await stream.CopyToAsync(fileStream, cancellationToken);
        }

        try
        {
            await foreach (var line in RunScriptAsync(tempFile, cancellationToken))
            {
                yield return line;
            }
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { }
            }
        }
    }

    // -- Modificação (DIDÁTICA): Refatoração Assíncrona e Thread Safety --
    // O uso de IAsyncEnumerable permite retornar dados contínuos (um fluxo ou stream)
    // à medida que o script gera novas linhas de log, tudo de forma assíncrona.
    public async IAsyncEnumerable<string> RunScriptAsync(string scriptPath, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // 1. O Channel atua como uma fila segura (thread-safe) para comunicação entre a thread que
        // está lendo as saídas do processo e a thread que está processando o IAsyncEnumerable.
        var channel = Channel.CreateUnbounded<string>();

        var processStartInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };

        using var process = new Process { StartInfo = processStartInfo };

        // 2. Assinamos os eventos para capturar o que o PowerShell imprime no console.
        // O TryWrite coloca a mensagem na fila de forma não-bloqueante.
        process.OutputDataReceived += (sender, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
                channel.Writer.TryWrite(args.Data);
        };

        process.ErrorDataReceived += (sender, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
                channel.Writer.TryWrite($"[ERRO] {args.Data}");
        };

        // 3. Iniciamos o processo e mandamos ele começar a ler as saídas assincronamente.
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // 4. USANDO TASK.RUN (A Mágica da Assincronicidade!):
        // Nós jogamos o comando "WaitForExitAsync" (que aguarda o processo terminar)
        // para rodar em uma Thread separada (Thread Pool) através do Task.Run().
        // Por que fazemos isso? Se aguardássemos diretamente na Thread da Interface (UI), 
        // a janela congelaria até o script terminar. Com Task.Run, a UI continua livre.
        _ = Task.Run(async () =>
        {
            try
            {
                // Await garante que essa Task espere o PowerShell fechar antes de continuar.
                await process.WaitForExitAsync(cancellationToken);
            }
            finally
            {
                // Quando terminar, avisamos a fila (channel) que não haverá mais mensagens.
                channel.Writer.Complete();
            }
        }, cancellationToken);

        // 5. YIELD RETURN (Retorno progressivo):
        // Aqui lemos os itens da fila conforme eles chegam e os "devolvemos" (yield) para a ViewModel.
        // O "await foreach" aguarda as novas linhas chegarem da fila SEM travar a UI!
        await foreach (var line in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return line;
        }
    }
}
