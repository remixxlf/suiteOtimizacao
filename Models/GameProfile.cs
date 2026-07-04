// -----------------------------------------------------------------------
// GameProfile.cs — Perfil de configuração por jogo
// Projeto: CoreIsolator
// -----------------------------------------------------------------------

namespace CoreIsolator.Models;

/// <summary>
/// Nível de prioridade a ser aplicado ao processo do jogo via API do Windows.
/// </summary>
/// <remarks>
/// Os valores correspondem às constantes utilizadas em
/// <c>SetPriorityClass</c> da API Win32. Níveis mais altos garantem
/// maior fatia de tempo de CPU para o processo do jogo.
/// </remarks>
public enum ProcessPriorityLevel
{
    /// <summary>
    /// Prioridade ociosa — o processo só executa quando a CPU está completamente livre.
    /// </summary>
    Idle,

    /// <summary>
    /// Prioridade abaixo do normal — menor que a maioria dos processos do sistema.
    /// </summary>
    BelowNormal,

    /// <summary>
    /// Prioridade normal — padrão do sistema operacional.
    /// </summary>
    Normal,

    /// <summary>
    /// Prioridade acima do normal — leve vantagem sobre processos comuns.
    /// </summary>
    AboveNormal,

    /// <summary>
    /// Prioridade alta — reserva mais tempo de CPU para o processo.
    /// Recomendado para jogos competitivos que exigem baixa latência.
    /// </summary>
    High
}

/// <summary>
/// Representa o perfil de otimização de um jogo específico,
/// definindo como o CoreIsolator deve gerenciar a afinidade de CPU
/// e a prioridade do processo quando o jogo estiver em execução.
/// </summary>
/// <remarks>
/// Cada perfil permite configurar:
/// <list type="bullet">
///   <item>Se o jogo deve ser fixado exclusivamente nos P-Cores;</item>
///   <item>Qual nível de prioridade aplicar ao processo;</item>
///   <item>Quais processos em segundo plano devem ser movidos para os E-Cores.</item>
/// </list>
/// </remarks>
public class GameProfile
{
    /// <summary>
    /// Nome amigável do jogo exibido na interface do usuário.
    /// </summary>
    /// <example>"Valorant", "Counter-Strike 2", "League of Legends"</example>
    public string GameName { get; set; } = string.Empty;

    /// <summary>
    /// Nome do arquivo executável do jogo (com extensão).
    /// Utilizado para detectar automaticamente quando o jogo está em execução.
    /// </summary>
    /// <example>"valorant.exe", "cs2.exe", "LeagueClient.exe"</example>
    public string ExecutableName { get; set; } = string.Empty;

    /// <summary>
    /// Indica se o jogo deve ser fixado exclusivamente nos P-Cores.
    /// Quando <c>true</c>, a máscara de afinidade do processo é definida
    /// para utilizar apenas núcleos de desempenho.
    /// </summary>
    public bool UsePCoresOnly { get; set; } = true;

    /// <summary>
    /// Nível de prioridade a ser aplicado ao processo do jogo.
    /// O padrão é <see cref="ProcessPriorityLevel.High"/> para maximizar
    /// o tempo de CPU dedicado ao jogo.
    /// </summary>
    public ProcessPriorityLevel Priority { get; set; } = ProcessPriorityLevel.High;

    /// <summary>
    /// Lista de nomes de executáveis de processos em segundo plano que devem
    /// ser movidos para os E-Cores enquanto este jogo estiver ativo.
    /// </summary>
    /// <remarks>
    /// Esses processos terão sua afinidade de CPU alterada para utilizar
    /// apenas os núcleos de eficiência, liberando os P-Cores inteiramente
    /// para o jogo. A afinidade original é restaurada quando o jogo é fechado
    /// (se configurado em <see cref="AppSettings.RestoreAffinityOnGameClose"/>).
    /// </remarks>
    public List<string> BackgroundProcesses { get; set; } =
    [
        "discord.exe",
        "chrome.exe",
        "msedge.exe",
        "obs64.exe",
        "spotify.exe",
        "brave.exe",
        "opera.exe",
        "streamlabs.exe"
    ];

    /// <summary>
    /// Indica se este perfil está ativo e deve ser monitorado.
    /// Perfis desabilitados são ignorados pelo serviço de monitoramento.
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}
