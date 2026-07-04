using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using CoreIsolator.Models;
using CoreIsolator.Services;

namespace CoreIsolator.Views;

/// <summary>
/// Lógica de interação para a janela principal do CoreIsolator.
/// Gerencia a UI, eventos de usuário e comunicação com o CoreIsolatorEngine.
/// </summary>
public partial class MainWindow : Window
{
    private CoreIsolatorEngine? _engine;
    private ProfileManager? _profileManager;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private void TweaksButton_Click(object sender, RoutedEventArgs e)
    {
        var tweaksWindow = App.AppHost!.Services.GetRequiredService<TweaksWindow>();
        tweaksWindow.Owner = this;
        tweaksWindow.ShowDialog();
    }

    /// <summary>
    /// Inicializa o engine e configura a UI ao carregar a janela.
    /// </summary>
    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _profileManager = new ProfileManager();
            _engine = new CoreIsolatorEngine(_profileManager);

            // Assinar eventos do engine
            _engine.GameDetected += OnGameDetected;
            _engine.GameClosed += OnGameClosed;
            _engine.StatusChanged += OnStatusChanged;
            _engine.LogMessage += OnLogMessage;

            // Inicializar o engine (detecta topologia, inicia monitoramento)
            _engine.Initialize();

            // Atualizar UI com dados da topologia
            UpdateTopologyDisplay(_engine.Topology);

            // Carregar perfis na lista
            RefreshProfileList();

            // Carregar configurações na UI
            LoadSettingsToUi(_engine.Settings);

            // Exibir máscaras na barra de status
            UpdateMaskInfo(_engine.Topology);

            AddLogEntry("CoreIsolator inicializado com sucesso");
            AddLogEntry($"CPU: {_engine.Topology.CpuName}");
            AddLogEntry($"Topologia: {_engine.Topology.PCores.Count} P-Cores, {_engine.Topology.ECores.Count} E-Cores");

            if (!_engine.Topology.IsHybrid)
            {
                AddLogEntry("⚠ CPU homogênea — apenas prioridade será gerenciada");
            }

            AddLogEntry("Monitoramento de processos ativo...");
        }
        catch (Exception ex)
        {
            AddLogEntry($"ERRO na inicialização: {ex.Message}");
            MessageBox.Show(
                $"Erro ao inicializar o CoreIsolator:\n\n{ex.Message}\n\nCertifique-se de executar como Administrador.",
                "CoreIsolator — Erro",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    // ═══════════════════════════════════════════════════════
    //              ATUALIZAÇÃO DA INTERFACE
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Atualiza o painel de visualização da topologia da CPU.
    /// Gera blocos visuais para cada núcleo (P-Core em azul, E-Core em verde).
    /// </summary>
    private void UpdateTopologyDisplay(CpuTopology topology)
    {
        CpuNameText.Text = topology.CpuName;
        PCoreCountText.Text = topology.PCores.Count.ToString();
        ECoreCountText.Text = topology.ECores.Count.ToString();
        ThreadCountText.Text = topology.TotalLogicalProcessors.ToString();

        // Gerar visualização dos núcleos
        CoreVisualizationPanel.Children.Clear();

        foreach (var core in topology.AllCores)
        {
            var isPCore = core.Type == CoreType.PCore;
            var gradient = isPCore
                ? (Brush)FindResource("PCoreBadgeGradient")
                : (Brush)FindResource("ECoreBadgeGradient");

            var corePanel = new StackPanel
            {
                Margin = new Thickness(2),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // Bloco principal do core
            var coreBorder = new Border
            {
                Width = 48,
                Height = 48,
                CornerRadius = new CornerRadius(8),
                Background = gradient,
                ToolTip = $"{(isPCore ? "P-Core" : "E-Core")} #{core.CoreId}\n" +
                          $"EfficiencyClass: {core.EfficiencyClass}\n" +
                          $"Máscara: 0x{core.AffinityMask:X}\n" +
                          $"Threads: {core.LogicalProcessors.Length}\n" +
                          $"SMT: {(core.HasSmt ? "Sim" : "Não")}",
                Child = new TextBlock
                {
                    Text = $"{(isPCore ? "P" : "E")}{core.CoreId}",
                    Foreground = (Brush)FindResource("CrustBrush"),
                    FontWeight = FontWeights.Bold,
                    FontSize = 13,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };

            corePanel.Children.Add(coreBorder);

            // Indicadores de threads lógicos (HT)
            if (core.LogicalProcessors.Length > 1)
            {
                var threadPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 3, 0, 0)
                };

                foreach (var lp in core.LogicalProcessors)
                {
                    threadPanel.Children.Add(new Ellipse
                    {
                        Width = 6,
                        Height = 6,
                        Fill = (Brush)FindResource("Surface2Brush"),
                        Margin = new Thickness(1, 0, 1, 0),
                        ToolTip = $"Thread Lógico #{lp}"
                    });
                }

                corePanel.Children.Add(threadPanel);
            }

            CoreVisualizationPanel.Children.Add(corePanel);
        }
    }

    /// <summary>
    /// Atualiza as informações de máscara na barra de status.
    /// </summary>
    private void UpdateMaskInfo(CpuTopology topology)
    {
        MaskInfoText.Text = topology.IsHybrid
            ? $"P-Mask: 0x{topology.PCoreMask:X}  |  E-Mask: 0x{topology.ECoreMask:X}"
            : $"All-Mask: 0x{topology.AllCoreMask:X}";
    }

    /// <summary>
    /// Carrega as configurações atuais na interface.
    /// </summary>
    private void LoadSettingsToUi(AppSettings settings)
    {
        AutoStartCheckBox.IsChecked = settings.AutoStartWithWindows;
        RestoreAffinityCheckBox.IsChecked = settings.RestoreAffinityOnGameClose;
        ShowNotificationsCheckBox.IsChecked = settings.ShowNotifications;
        MinimizeOnStartCheckBox.IsChecked = settings.MinimizeToTrayOnStart;
    }

    /// <summary>
    /// Recarrega a lista de perfis na ListBox.
    /// </summary>
    private void RefreshProfileList()
    {
        if (_engine?.Settings?.Profiles != null)
        {
            ProfileListBox.ItemsSource = null;
            ProfileListBox.ItemsSource = _engine.Settings.Profiles;
        }
    }

    // ═══════════════════════════════════════════════════════
    //               EVENTOS DO ENGINE
    // ═══════════════════════════════════════════════════════

    private void OnGameDetected(string gameName)
    {
        Dispatcher.Invoke(() =>
        {
            ActiveGameCard.Visibility = Visibility.Visible;
            ActiveGameNameText.Text = gameName;
            StatusBadge.Background = new SolidColorBrush((Color)FindResource("GreenAccentColor"));
            StatusBadgeText.Text = "🟢 Ativo";
            StatusBadgeText.Foreground = (Brush)FindResource("GreenAccentBrush");
            StatusBarText.Text = $"Jogo isolado: {gameName}";

            if (_engine?.Topology != null)
            {
                ActiveAffinityText.Text = $"0x{_engine.Topology.PCoreMask:X}";
            }

            ActivePriorityText.Text = "HIGH";
        });
    }

    private void OnGameClosed(string gameName)
    {
        Dispatcher.Invoke(() =>
        {
            ActiveGameCard.Visibility = Visibility.Collapsed;
            StatusBadge.Background = (Brush)FindResource("Surface1Brush");
            StatusBadgeText.Text = "⏸ Idle";
            StatusBadgeText.Foreground = (Brush)FindResource("SubtextBrush");
            StatusBarText.Text = "Monitoramento ativo — aguardando jogo...";
        });
    }

    private void OnStatusChanged(string status)
    {
        Dispatcher.Invoke(() =>
        {
            StatusBarText.Text = status;
        });
    }

    private void OnLogMessage(string message)
    {
        Dispatcher.Invoke(() =>
        {
            AddLogEntry(message);
        });
    }

    // ═══════════════════════════════════════════════════════
    //              EVENTOS DE UI (BOTÕES)
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Adiciona um novo perfil de jogo à lista.
    /// </summary>
    private void AddProfile_Click(object sender, RoutedEventArgs e)
    {
        var gameName = NewGameNameBox.Text.Trim();
        var exeName = NewGameExeBox.Text.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(gameName) || string.IsNullOrWhiteSpace(exeName))
        {
            AddLogEntry("⚠ Preencha o nome do jogo e o executável");
            return;
        }

        // Garantir extensão .exe
        if (!exeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            exeName += ".exe";

        var profile = new GameProfile
        {
            GameName = gameName,
            ExecutableName = exeName,
            UsePCoresOnly = true,
            Priority = ProcessPriorityLevel.High,
            BackgroundProcesses = _engine?.Settings.DefaultBackgroundProcesses?.ToList()
                ?? ["discord.exe", "chrome.exe", "obs64.exe", "spotify.exe"]
        };

        _profileManager?.AddProfile(profile);
        RefreshProfileList();

        NewGameNameBox.Clear();
        NewGameExeBox.Clear();

        AddLogEntry($"✅ Perfil adicionado: {gameName} ({exeName})");

        // Fazer uma varredura para ver se o jogo já está aberto
        _engine?.CheckForRunningGames();
    }

    /// <summary>
    /// Remove o perfil selecionado da lista.
    /// </summary>
    private void RemoveProfile_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileListBox.SelectedItem is GameProfile selected)
        {
            _profileManager?.RemoveProfile(selected.ExecutableName);
            RefreshProfileList();
            AddLogEntry($"🗑 Perfil removido: {selected.GameName}");
        }
        else
        {
            AddLogEntry("⚠ Selecione um perfil para remover");
        }
    }

    /// <summary>
    /// Limpa o log de atividade.
    /// </summary>
    private void ClearLog_Click(object sender, RoutedEventArgs e)
    {
        LogListBox.Items.Clear();
    }

    // ═══════════════════════════════════════════════════════
    //            EVENTOS DE CONFIGURAÇÃO
    // ═══════════════════════════════════════════════════════

    private void AutoStartCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_engine?.Settings == null || _profileManager == null) return;

        var isChecked = AutoStartCheckBox.IsChecked == true;
        _engine.Settings.AutoStartWithWindows = isChecked;
        _profileManager.SaveSettings(_engine.Settings);

        try
        {
            if (isChecked)
                AutoStartManager.EnableAutoStart();
            else
                AutoStartManager.DisableAutoStart();

            AddLogEntry(isChecked
                ? "✅ Inicialização automática ativada (Agendador de Tarefas)"
                : "❌ Inicialização automática desativada");
        }
        catch (Exception ex)
        {
            AddLogEntry($"⚠ Erro ao configurar auto-start: {ex.Message}");
        }
    }

    private void RestoreAffinityCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_engine?.Settings == null || _profileManager == null) return;
        _engine.Settings.RestoreAffinityOnGameClose = RestoreAffinityCheckBox.IsChecked == true;
        _profileManager.SaveSettings(_engine.Settings);
    }

    private void ShowNotificationsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_engine?.Settings == null || _profileManager == null) return;
        _engine.Settings.ShowNotifications = ShowNotificationsCheckBox.IsChecked == true;
        _profileManager.SaveSettings(_engine.Settings);
    }

    private void MinimizeOnStartCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_engine?.Settings == null || _profileManager == null) return;
        _engine.Settings.MinimizeToTrayOnStart = MinimizeOnStartCheckBox.IsChecked == true;
        _profileManager.SaveSettings(_engine.Settings);
    }

    // ═══════════════════════════════════════════════════════
    //          CONTROLES DA JANELA (TÍTULO CUSTOM)
    // ═══════════════════════════════════════════════════════

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

    // ═══════════════════════════════════════════════════════
    //                   LOG DE ATIVIDADE
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Adiciona uma entrada timestamped ao log de atividade.
    /// </summary>
    private void AddLogEntry(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var entry = $"[{timestamp}] {message}";

        LogListBox.Items.Add(entry);

        // Auto-scroll para o final
        if (LogListBox.Items.Count > 0)
        {
            LogListBox.ScrollIntoView(LogListBox.Items[^1]);
        }

        // Limitar a 200 entradas para não consumir memória
        while (LogListBox.Items.Count > 200)
        {
            LogListBox.Items.RemoveAt(0);
        }
    }

    /// <summary>
    /// Cleanup ao fechar a aplicação.
    /// </summary>
    protected override void OnClosed(EventArgs e)
    {
        _engine?.Dispose();
        base.OnClosed(e);
    }
}
