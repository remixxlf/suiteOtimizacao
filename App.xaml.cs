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
                services.AddSingleton<MainWindow>();
                services.AddTransient<TweaksViewModel>();
                services.AddTransient<TweaksWindow>();
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

        // Criar e exibir a janela principal via Injeção de Dependência
        _mainWindow = AppHost.Services.GetRequiredService<MainWindow>();
        _mainWindow.Show();
    }

    /// <summary>
    /// Evento do menu de contexto da bandeja: Mostrar janela principal.
    /// </summary>
    private void ShowWindow_Click(object sender, RoutedEventArgs e)
    {
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
