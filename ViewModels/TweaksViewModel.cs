using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CoreIsolator.Services;

namespace CoreIsolator.ViewModels;

public partial class TweaksViewModel : ObservableObject
{
    private readonly IPowerShellRunnerService _powerShellRunner;
    private readonly ITelemetryClient _telemetryClient;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private bool _isOptimizing;

    public TweaksViewModel(IPowerShellRunnerService powerShellRunner, ITelemetryClient telemetryClient)
    {
        _powerShellRunner = powerShellRunner;
        _telemetryClient = telemetryClient;
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task OptimizeAsync(CancellationToken cancellationToken)
    {
        IsOptimizing = true;
        StatusText = "Iniciando processo de otimização de sistema...\n\n";
        var logBuilder = new StringBuilder();

        try
        {
            string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "Otimizador_Windows.ps1");

            await foreach (var line in _powerShellRunner.RunScriptAsync(scriptPath, cancellationToken))
            {
                StatusText += line + Environment.NewLine;
                logBuilder.AppendLine(line);
            }

            StatusText += "\n✅ Otimização concluída com sucesso!";
        }
        catch (OperationCanceledException)
        {
            StatusText += "\n🛑 Operação cancelada pelo usuário.";
            logBuilder.AppendLine("Operação abortada.");
        }
        catch (Exception ex)
        {
            StatusText += $"\n❌ Erro crítico: {ex.Message}";
            logBuilder.AppendLine($"ERROR: {ex.Message}");
        }
        finally
        {
            IsOptimizing = false;

            var payload = new
            {
                Timestamp = DateTime.UtcNow,
                MachineName = Environment.MachineName,
                Environment = "WPF_Desktop",
                Success = !logBuilder.ToString().Contains("ERROR:"),
                TotalLogsLength = logBuilder.Length
            };

            await _telemetryClient.SendOptimizationReportAsync(payload);
        }
    }
}
