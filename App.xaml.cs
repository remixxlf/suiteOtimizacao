using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using H.NotifyIcon;
using CoreIsolator.Views;
using CoreIsolator.Services;
using CoreIsolator.ViewModels;

namespace CoreIsolator;

/// <summary>
/// Ponto de entrada da aplicação CoreIsolator.
/// Gerencia o ciclo de vida da aplicação, ícone da bandeja do sistema,
/// e a janela principal.
/// </summary>
public partial class App : Application
{
    private TaskbarIcon? _trayIcon;
    private MainWindow? _mainWindow;

    public static IHost? AppHost { get; private set; }

    public App()
    {
        AppHost = Host.CreateDefaultBuilder()
            .ConfigureServices((hostContext, services) =>
            {
                // -- Modificação (DIDÁTICA): Registro de Serviços (Dependency Injection) --
                // O ServiceCollection atua como um contêiner. Registramos nossas classes aqui para que o 
                // framework saiba como criá-las quando precisarmos delas.
                
                // AddSingleton: Cria uma única instância para toda a vida da aplicação. Útil para gerenciadores de estado.
                services.AddSingleton<ProfileManager>();
                services.AddSingleton<CoreIsolatorEngine>();
                
                // AddTransient: Cria uma nova instância toda vez que for solicitado. Útil para Janelas e ViewModels independentes.
                services.AddTransient<MainViewModel>();
                services.AddTransient<MainWindow>();
                services.AddTransient<TweaksViewModel>();
                services.AddTransient<TweaksWindow>();
                
                // Interface para Implementação: Sempre que alguém pedir um IPowerShellRunnerService, entregamos um PowerShellRunnerService.
                services.AddTransient<IPowerShellRunnerService, PowerShellRunnerService>();
                
                services.AddHttpClient<ITelemetryClient, TelemetryClient>(client =>
                {
                    client.BaseAddress = new Uri("https://otimiza-ao-api.vercel.app/"); // URL de exemplo
                    client.Timeout = TimeSpan.FromSeconds(15); 
                });
            })
            .Build();
    }

    /// <summary>
    /// Inicializa a aplicação, configura o ícone da bandeja e abre a janela principal.
    /// </summary>
    protected override async void OnStartup(StartupEventArgs e)
    {
        await AppHost!.StartAsync();
        
        base.OnStartup(e);

        // Inicializar o ícone da bandeja do sistema
        _trayIcon = (TaskbarIcon)FindResource("TrayIcon");

        // -- Modificação (DIDÁTICA): Forçando a renderização do ícone --
        // Como instanciamos o TaskbarIcon dentro de um ResourceDictionary (Application.Resources),
        // ele não está contido numa Janela ativa, então o Windows pode não saber que deve exibi-lo.
        // O ForceCreate() diz diretamente à API do Windows: "Ei, construa este ícone na bandeja agora mesmo!".
        _trayIcon?.ForceCreate();

        // Criar e exibir a janela principal via Injeção de Dependência
        _mainWindow = AppHost.Services.GetRequiredService<MainWindow>();
        _mainWindow.Show();
    }

    /// <summary>
    /// Evento do menu de contexto da bandeja: Mostrar janela principal.
    /// </summary>
    private void ShowWindow_Click(object sender, RoutedEventArgs e)
    {
        // -- Modificação (DIDÁTICA): Restauração da Janela --
        // Quando minimizamos para a bandeja, apenas escondemos a janela (Hide() e ShowInTaskbar = false).
        // Para trazê-la de volta, chamamos o método RestoreWindow() que nós mesmos criamos na MainWindow,
        // que se encarrega de dar o Show(), reativar a visibilidade na barra de tarefas (ShowInTaskbar = true) e focar (Activate).
        if (_mainWindow != null)
        {
            _mainWindow.RestoreWindow();
        }
    }

    /// <summary>
    /// Evento do menu de contexto da bandeja: Sair da aplicação.
    /// </summary>
    private void ExitApp_Click(object sender, RoutedEventArgs e)
    {
        // Limpar o ícone da bandeja para evitar ícone fantasma
        _trayIcon?.Dispose();
        _trayIcon = null;

        // Fechar a janela principal (dispara OnClosed que faz cleanup do engine)
        _mainWindow?.Close();

        // Encerrar a aplicação
        Current.Shutdown();
    }

    /// <summary>
    /// Clique esquerdo no ícone da bandeja: Restaurar/mostrar janela.
    /// </summary>
    private void TrayIcon_TrayLeftMouseDown(object sender, RoutedEventArgs e)
    {
        // -- Modificação (DIDÁTICA): Clique direto no ícone --
        // Reutilizamos a lógica de restaurar a janela. Assim, o usuário pode tanto usar
        // o Menu de Contexto (botão direito > Mostrar) quanto dar um simples clique esquerdo no ícone da bandeja.
        if (_mainWindow != null)
        {
            _mainWindow.RestoreWindow();
        }
    }

    /// <summary>
    /// Cleanup ao encerrar a aplicação.
    /// </summary>
    protected override async void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        await AppHost!.StopAsync();
        base.OnExit(e);
    }
}
