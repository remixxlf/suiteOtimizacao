using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace CoreIsolator.Services;

public interface ITelemetryClient
{
    Task SendOptimizationReportAsync(object payload);
}

public class TelemetryClient : ITelemetryClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TelemetryClient> _logger;

    public TelemetryClient(HttpClient httpClient, ILogger<TelemetryClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task SendOptimizationReportAsync(object payload)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/webhook", payload);
                response.EnsureSuccessStatusCode();
                _logger.LogInformation("Telemetria enviada com sucesso para a Vercel.");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Falha ao enviar telemetria. Rede indisponível ou endpoint inacessível.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao processar telemetria.");
            }
        });

        return Task.CompletedTask;
    }
}
