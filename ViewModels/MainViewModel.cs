using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CoreIsolator.Models;
using CoreIsolator.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CoreIsolator.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly CoreIsolatorEngine _engine;
    private readonly ProfileManager _profileManager;

    [ObservableProperty]
    private CpuTopology _topology = new();

    [ObservableProperty]
    private ObservableCollection<GameProfile> _profiles = new();

    [ObservableProperty]
    private AppSettings _settings = AppSettings.CreateDefault();

    [ObservableProperty]
    private string _activeGameName = string.Empty;

    [ObservableProperty]
    private bool _isActiveGameCardVisible;

    [ObservableProperty]
    private string _statusBadgeText = "⏸ Idle";

    [ObservableProperty]
    private string _statusBarText = "Monitoramento ativo — aguardando jogo...";

    [ObservableProperty]
    private string _activeAffinityText = string.Empty;

    [ObservableProperty]
    private string _maskInfoText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _logs = new();

    public MainViewModel(CoreIsolatorEngine engine, ProfileManager profileManager)
    {
        _engine = engine;
        _profileManager = profileManager;

        // Assinar eventos do engine
        _engine.GameDetected += OnGameDetected;
        _engine.GameClosed += OnGameClosed;
        _engine.StatusChanged += OnStatusChanged;
        _engine.LogMessage += OnLogMessage;

        // Inicializar o engine (detecta topologia, inicia monitoramento)
        try
        {
            _engine.Initialize();

            Topology = _engine.Topology;
            Settings = _engine.Settings;
            
            // Inicializar a lista de perfis para refletir no Binding
            Profiles = new ObservableCollection<GameProfile>(_engine.Settings.Profiles);

            UpdateMaskInfo(_engine.Topology);

            AddLogEntry("CoreIsolator inicializado com sucesso via MVVM");
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
        }
    }

    private void UpdateMaskInfo(CpuTopology topology)
    {
        MaskInfoText = topology.IsHybrid
            ? $"P-Mask: 0x{topology.PCoreMask:X}  |  E-Mask: 0x{topology.ECoreMask:X}"
            : $"All-Mask: 0x{topology.AllCoreMask:X}";
    }

    private void OnGameDetected(string gameName)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            IsActiveGameCardVisible = true;
            ActiveGameName = gameName;
            StatusBadgeText = "🟢 Ativo";
            StatusBarText = $"Jogo isolado: {gameName}";

            if (_engine.Topology != null)
            {
                ActiveAffinityText = $"0x{_engine.Topology.PCoreMask:X}";
            }
        });
    }

    private void OnGameClosed(string gameName)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            IsActiveGameCardVisible = false;
            StatusBadgeText = "⏸ Idle";
            StatusBarText = "Monitoramento ativo — aguardando jogo...";
        });
    }

    private void OnStatusChanged(string status)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            StatusBarText = status;
        });
    }

    private void OnLogMessage(string message)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            AddLogEntry(message);
        });
    }

    private void AddLogEntry(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var entry = $"[{timestamp}] {message}";
        
        Logs.Add(entry);
        
        if (Logs.Count > 200)
        {
            Logs.RemoveAt(0);
        }
    }

    [ObservableProperty]
    private string _newProfileName = string.Empty;

    [ObservableProperty]
    private string _newProfileExe = string.Empty;

    [ObservableProperty]
    private GameProfile? _selectedProfile;

    [RelayCommand]
    private void AddProfile()
    {
        if (string.IsNullOrWhiteSpace(NewProfileName) || string.IsNullOrWhiteSpace(NewProfileExe))
        {
            AddLogEntry("⚠ Preencha o nome do jogo e o executável");
            return;
        }

        var exeName = NewProfileExe.Trim().ToLowerInvariant();
        if (!exeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            exeName += ".exe";

        var profile = new GameProfile
        {
            GameName = NewProfileName.Trim(),
            ExecutableName = exeName,
            UsePCoresOnly = true,
            Priority = ProcessPriorityLevel.High,
            BackgroundProcesses = _engine.Settings.DefaultBackgroundProcesses?.ToList()
                ?? ["discord.exe", "chrome.exe", "obs64.exe", "spotify.exe"]
        };

        _profileManager.AddProfile(profile);
        Profiles.Clear();
        foreach (var p in _engine.Settings.Profiles) Profiles.Add(p);

        AddLogEntry($"✅ Perfil adicionado: {profile.GameName} ({exeName})");
        
        // Limpar inputs
        NewProfileName = string.Empty;
        NewProfileExe = string.Empty;
        
        _engine.CheckForRunningGames();
    }

    [RelayCommand]
    private void RemoveProfile()
    {
        if (SelectedProfile != null)
        {
            _profileManager.RemoveProfile(SelectedProfile.ExecutableName);
            var gameName = SelectedProfile.GameName;
            Profiles.Remove(SelectedProfile);
            SelectedProfile = null;
            AddLogEntry($"🗑 Perfil removido: {gameName}");
        }
        else
        {
            AddLogEntry("⚠ Selecione um perfil para remover");
        }
    }

    [RelayCommand]
    private void ClearLog()
    {
        Logs.Clear();
    }

    [RelayCommand]
    private void OpenTweaksWindow()
    {
        var tweaksWindow = App.AppHost?.Services.GetRequiredService<CoreIsolator.Views.TweaksWindow>();
        if (tweaksWindow != null)
        {
            tweaksWindow.Owner = System.Windows.Application.Current.MainWindow;
            tweaksWindow.ShowDialog();
        }
    }

    public void SaveSettings()
    {
        _profileManager.SaveSettings(Settings);
    }

    public void Dispose()
    {
        _engine.GameDetected -= OnGameDetected;
        _engine.GameClosed -= OnGameClosed;
        _engine.StatusChanged -= OnStatusChanged;
        _engine.LogMessage -= OnLogMessage;
        _engine.Dispose();
    }
}
