using System.Windows;
using System.Windows.Input;
using CoreIsolator.ViewModels;

namespace CoreIsolator.Views;

public partial class TweaksWindow : Window
{
    public TweaksWindow(TweaksViewModel viewModel)
    {
        InitializeComponent();
        
        // Atribui o DataContext para fazer o binding da ViewModel com a View
        DataContext = viewModel;
        
        // Auto-scroll para o log
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(TweaksViewModel.StatusText))
            {
                LogScrollViewer.ScrollToEnd();
            }
        };
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
