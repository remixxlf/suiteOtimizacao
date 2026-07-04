// -----------------------------------------------------------------------
// AppSettings.cs — Configurações globais da aplicação
// Projeto: CoreIsolator
// -----------------------------------------------------------------------

namespace CoreIsolator.Models;

/// <summary>
/// Representa as configurações persistentes da aplicação CoreIsolator.
/// </summary>
/// <remarks>
/// Esta classe é serializada/desserializada em JSON para manter as
/// preferências do usuário entre sessões. Utilize <see cref="CreateDefault"/>
/// para gerar uma instância com valores padrão sensatos na primeira execução.
/// </remarks>
public class AppSettings
{
    /// <summary>
    /// Lista de perfis de jogos configurados pelo usuário.
    /// Cada perfil define regras de afinidade e prioridade para um jogo específico.
    /// </summary>
    public List<GameProfile> Profiles { get; set; } = [];

    /// <summary>
    /// Lista padrão de processos em segundo plano que podem ser relegados
    /// aos E-Cores. Serve como modelo para novos perfis de jogos criados
    /// pelo usuário.
    /// </summary>
    /// <remarks>
    /// O usuário pode personalizar esta lista nas configurações globais.
    /// Novos perfis criados herdarão esta lista como ponto de partida.
    /// </remarks>
    public List<string> DefaultBackgroundProcesses { get; set; } =
    [
        "discord.exe",
        "chrome.exe",
        "msedge.exe",
        "obs64.exe",
        "spotify.exe",
        "brave.exe",
        "opera.exe",
        "streamlabs.exe",
        "medal.exe"
    ];

    /// <summary>
    /// Indica se a aplicação deve iniciar automaticamente com o Windows.
    /// Quando <c>true</c>, uma entrada é criada no registro de inicialização.
    /// </summary>
    public bool AutoStartWithWindows { get; set; } = false;

    /// <summary>
    /// Indica se a aplicação deve minimizar para a bandeja do sistema
    /// (system tray) ao ser iniciada, em vez de exibir a janela principal.
    /// </summary>
    public bool MinimizeToTrayOnStart { get; set; } = true;

    /// <summary>
    /// Indica se a afinidade de CPU dos processos em segundo plano deve
    /// ser restaurada ao valor original quando o jogo for fechado.
    /// </summary>
    /// <remarks>
    /// Recomenda-se manter esta opção habilitada para evitar que processos
    /// fiquem permanentemente restritos aos E-Cores após a sessão de jogo.
    /// </remarks>
    public bool RestoreAffinityOnGameClose { get; set; } = true;

    /// <summary>
    /// Indica se notificações toast devem ser exibidas quando o CoreIsolator
    /// detecta um jogo e aplica otimizações de afinidade.
    /// </summary>
    public bool ShowNotifications { get; set; } = true;

    /// <summary>
    /// Versão do esquema de configurações, utilizada para migrações futuras.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Cria uma nova instância de <see cref="AppSettings"/> com valores
    /// padrão sensatos para a primeira execução da aplicação.
    /// </summary>
    /// <returns>
    /// Uma instância de <see cref="AppSettings"/> pré-configurada com
    /// a lista padrão de processos em segundo plano, notificações habilitadas,
    /// minimização para bandeja ativa e sem perfis de jogos configurados.
    /// </returns>
    public static AppSettings CreateDefault()
    {
        return new AppSettings
        {
            Profiles = [],
            DefaultBackgroundProcesses =
            [
                "discord.exe",
                "chrome.exe",
                "msedge.exe",
                "obs64.exe",
                "spotify.exe",
                "brave.exe",
                "opera.exe",
                "streamlabs.exe",
                "medal.exe"
            ],
            AutoStartWithWindows = false,
            MinimizeToTrayOnStart = true,
            RestoreAffinityOnGameClose = true,
            ShowNotifications = true,
            Version = "1.0.0"
        };
    }
}
