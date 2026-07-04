using System.Windows;
using System.Windows.Input;
using CoreIsolator.ViewModels;

namespace CoreIsolator.Views;

/// <summary>
/// Lógica de interação para a janela principal do CoreIsolator.
/// Agora utiliza MVVM estrito: O Code-Behind é responsável Apenas por regras puramente visuais,
/// como mover e minimizar a janela. Toda a lógica de negócio foi extraída para o MainViewModel.
/// </summary>
public partial class MainWindow : Window
{
    // -- Modificação (DIDÁTICA): Injeção do ViewModel no Construtor --
    // O sistema de Injeção de Dependência (configurado em App.xaml.cs) passa a instância 
    // correta de MainViewModel para o construtor da janela automaticamente.
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        
        // DataContext é o que permite ao XAML saber de onde tirar os dados (Bindings).
        // Aqui conectamos a View (MainWindow) com a sua ViewModel (MainViewModel).
        DataContext = viewModel;
        
        // Mantemos o fechamento suave chamando Dispose na ViewModel ao fechar.
        Closed += (s, e) => viewModel.Dispose();
    }

    // ═══════════════════════════════════════════════════════
    //          CONTROLES DA JANELA (TÍTULO CUSTOM)
    // ═══════════════════════════════════════════════════════
    // Nota didática: Estes eventos permanecem no code-behind pois lidam estritamente 
    // com comportamento visual da janela do sistema operacional.

    /// <summary>
    /// Permite arrastar a janela pela barra de título customizada.
    /// </summary>
    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    /// <summary>
    /// Minimiza para a bandeja do sistema ao invés de fechar.
    /// </summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        ShowInTaskbar = false;
    }

    /// <summary>
    /// Minimiza a janela.
    /// </summary>
    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    /// <summary>
    /// Ao minimizar, esconde a janela (minimiza para a bandeja).
    /// </summary>
    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState == WindowState.Minimized)
        {
            Hide();
            ShowInTaskbar = false;
        }
    }

    /// <summary>
    /// Restaura a janela a partir da bandeja do sistema.
    /// </summary>
    public void RestoreWindow()
    {
        Show();
        ShowInTaskbar = true;
        WindowState = WindowState.Normal;
        Activate();
    }
}
