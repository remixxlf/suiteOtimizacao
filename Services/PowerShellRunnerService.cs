using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace CoreIsolator.Services;

public interface IPowerShellRunnerService
{
    IAsyncEnumerable<string> RunScriptAsync(string scriptPath, CancellationToken cancellationToken = default);
}

public class PowerShellRunnerService : IPowerShellRunnerService
{
    public async IAsyncEnumerable<string> RunScriptAsync(string scriptPath, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateUnbounded<string>();

        var processStartInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = processStartInfo };

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

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        _ = Task.Run(async () =>
        {
            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            finally
            {
                channel.Writer.Complete();
            }
        }, cancellationToken);

        await foreach (var line in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return line;
        }
    }
}
