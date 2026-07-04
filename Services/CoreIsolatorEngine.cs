using System.Diagnostics;
using CoreIsolator.Models;
using CoreIsolator.Native;

namespace CoreIsolator.Services;

/// <summary>
/// Motor principal do CoreIsolator — orquestra todos os serviços:
/// detecção de topologia, monitoramento de processos, manipulação de afinidade
/// e gerenciamento de perfis.
/// 
/// Fluxo de operação:
/// 1. Inicializa → Ativa SeDebugPrivilege → Detecta topologia da CPU
/// 2. Carrega perfis → Inicia monitoramento WMI de processos
/// 3. Ao detectar jogo cadastrado → Isola nos P-Cores + relega processos secundários
/// 4. Ao detectar encerramento do jogo → Restaura afinidades originais
/// </summary>
public sealed class CoreIsolatorEngine : IDisposable
{
    // ═══════════════════════════════════════════════════════
    //                    DEPENDÊNCIAS
    // ═══════════════════════════════════════════════════════

    private readonly ProfileManager _profileManager;
    private readonly AffinityManager _affinityManager;
    private ProcessWatcher? _processWatcher;
    private bool _disposed;

    // ═══════════════════════════════════════════════════════
    //                    PROPRIEDADES
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Topologia completa da CPU detectada (P-Cores, E-Cores, máscaras).
    /// </summary>
    public CpuTopology Topology { get; private set; } = new();

    /// <summary>
    /// Configurações atuais da aplicação (perfis, preferências).
    /// </summary>
    public AppSettings Settings { get; private set; } = AppSettings.CreateDefault();

    /// <summary>
    /// Indica se um jogo está atualmente isolado nos P-Cores.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Nome amigável do jogo atualmente ativo (null se nenhum).
    /// </summary>
    public string? ActiveGameName { get; private set; }

    /// <summary>
    /// PID do processo do jogo atualmente ativo (null se nenhum).
    /// </summary>
    public int? ActiveGamePid { get; private set; }

    // ═══════════════════════════════════════════════════════
    //                      EVENTOS
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Disparado quando um jogo cadastrado é detectado e isolado.
    /// Parâmetro: nome do jogo.
    /// </summary>
    public event Action<string>? GameDetected;

    /// <summary>
    /// Disparado quando o jogo ativo é encerrado e as afinidades são restauradas.
    /// Parâmetro: nome do jogo.
    /// </summary>
    public event Action<string>? GameClosed;

    /// <summary>
    /// Disparado quando o status do engine muda (para atualização da UI).
    /// Parâmetro: mensagem de status.
    /// </summary>
    public event Action<string>? StatusChanged;

    /// <summary>
    /// Disparado para cada mensagem de log gerada pelo engine.
    /// Parâmetro: mensagem de log formatada.
    /// </summary>
    public event Action<string>? LogMessage;

    // ═══════════════════════════════════════════════════════
    //                    CONSTRUTOR
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Cria uma nova instância do CoreIsolatorEngine.
    /// </summary>
    /// <param name="profileManager">Gerenciador de perfis para persistência de configurações.</param>
    public CoreIsolatorEngine(ProfileManager profileManager)
    {
        _profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));
        _affinityManager = new AffinityManager();
    }

    // ═══════════════════════════════════════════════════════
    //                   INICIALIZAÇÃO
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Inicializa todos os subsistemas do engine:
    /// 1. Ativa o privilégio SeDebugPrivilege para acesso a processos de terceiros
    /// 2. Detecta a topologia da CPU (P-Cores vs E-Cores)
    /// 3. Carrega as configurações e perfis de jogos
    /// 4. Inicia o monitoramento de processos via WMI
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Se não for possível ativar o SeDebugPrivilege (app não está como Administrador).
    /// </exception>
    public void Initialize()
    {
        // ── Passo 1: Elevar privilégios ──
        Log("Ativando SeDebugPrivilege...");
        StatusChanged?.Invoke("Ativando privilégios de sistema...");

        if (PrivilegeManager.EnableDebugPrivilege())
        {
            Log("✅ SeDebugPrivilege ativado com sucesso");
        }
        else
        {
            Log("⚠ Falha ao ativar SeDebugPrivilege — funcionalidade limitada");
            Log("  Certifique-se de executar como Administrador");
        }

        // ── Passo 2: Detectar topologia da CPU ──
        Log("Detectando topologia do processador...");
        StatusChanged?.Invoke("Detectando topologia da CPU...");

        try
        {
            Topology = TopologyDetector.Detect();
            Log($"✅ CPU detectada: {Topology.CpuName}");
            Log($"   P-Cores: {Topology.PCores.Count} | E-Cores: {Topology.ECores.Count} | Threads: {Topology.TotalLogicalProcessors}");
            Log($"   P-Mask: 0x{Topology.PCoreMask:X} | E-Mask: 0x{Topology.ECoreMask:X}");

            if (!Topology.IsHybrid)
            {
                Log("ℹ CPU homogênea detectada (sem E-Cores) — apenas prioridade será gerenciada");
            }
        }
        catch (Exception ex)
        {
            Log($"❌ Erro ao detectar topologia: {ex.Message}");
            throw;
        }

        // ── Passo 3: Carregar configurações ──
        Log("Carregando perfis de jogos...");
        StatusChanged?.Invoke("Carregando configurações...");

        try
        {
            Settings = _profileManager.LoadSettings();
            Log($"✅ {Settings.Profiles.Count} perfil(is) de jogo carregado(s)");

            foreach (var profile in Settings.Profiles)
            {
                Log($"   📋 {profile.GameName} ({profile.ExecutableName}) — {(profile.IsEnabled ? "ativo" : "desativado")}");
            }
        }
        catch (Exception ex)
        {
            Log($"⚠ Erro ao carregar configurações: {ex.Message}");
            Settings = AppSettings.CreateDefault();
            Log("   Usando configurações padrão");
        }

        // ── Passo 4: Iniciar monitoramento de processos ──
        Log("Iniciando monitoramento de processos...");
        StatusChanged?.Invoke("Iniciando monitoramento WMI...");

        try
        {
            _processWatcher = new ProcessWatcher();
            _processWatcher.ProcessStarted += OnProcessStarted;
            _processWatcher.ProcessStopped += OnProcessStopped;
            _processWatcher.Start();
            Log("✅ Monitoramento de processos ativo (WMI)");
        }
        catch (Exception ex)
        {
            Log($"❌ Erro ao iniciar monitoramento: {ex.Message}");
            throw;
        }

        StatusChanged?.Invoke("Monitoramento ativo — aguardando jogo...");
        Log("═══════════════════════════════════════════");
        Log("CoreIsolator pronto. Aguardando detecção de jogos...");

        // Verifica se o jogo já está aberto
        CheckForRunningGames();
    }

    /// <summary>
    /// Varre os processos atuais do sistema para ver se algum jogo configurado
    /// já está rodando (útil ao iniciar o app ou adicionar um novo perfil com o jogo já aberto).
    /// </summary>
    public void CheckForRunningGames()
    {
        if (IsActive) return;

        Log("🔍 Varrendo processos abertos por jogos conhecidos...");

        try
        {
            var runningProcesses = Process.GetProcesses();
            foreach (var profile in Settings.Profiles)
            {
                if (!profile.IsEnabled) continue;

                var exeNameWithoutExtension = profile.ExecutableName.Replace(".exe", "", StringComparison.OrdinalIgnoreCase);
                var match = runningProcesses.FirstOrDefault(p => string.Equals(p.ProcessName, exeNameWithoutExtension, StringComparison.OrdinalIgnoreCase));
                
                if (match != null)
                {
                    Log($"💡 Jogo já em execução encontrado: {profile.GameName} (PID: {match.Id})");
                    OnProcessStarted(profile.ExecutableName, match.Id);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Log($"⚠ Erro ao varrer processos abertos: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════
    //            HANDLERS DE EVENTOS DE PROCESSO
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Callback chamado pelo ProcessWatcher quando um novo processo é criado.
    /// Verifica se o processo corresponde a um perfil de jogo cadastrado
    /// e, se sim, aplica o isolamento de núcleos.
    /// </summary>
    private void OnProcessStarted(string processName, int pid)
    {
        // Verificar se já temos um jogo ativo (não sobrescrever)
        if (IsActive)
        {
            Debug.WriteLine($"[Engine] Processo '{processName}' ignorado — jogo '{ActiveGameName}' já ativo.");
            return;
        }

        // Procurar perfil correspondente ao processo
        var profile = FindMatchingProfile(processName);
        if (profile == null) return;

        Log($"🎮 Jogo detectado: {profile.GameName} ({processName}, PID: {pid})");
        StatusChanged?.Invoke($"Isolando: {profile.GameName}...");

        try
        {
            // Determinar a classe de prioridade com base no perfil
            uint priorityClass = GetPriorityClass(profile.Priority);

            if (Topology.IsHybrid && profile.UsePCoresOnly)
            {
                // ── CPU Híbrida: Isolamento completo ──
                // 1. Isolar o jogo nos P-Cores
                if (_affinityManager.IsolateProcess(pid, Topology.PCoreMask, priorityClass))
                {
                    Log($"   ✅ Jogo fixado nos P-Cores (Mask: 0x{Topology.PCoreMask:X}, Prioridade: {profile.Priority})");
                }
                else
                {
                    Log($"   ⚠ Falha ao definir afinidade do jogo (PID: {pid})");
                }

                // 2. Relegar processos secundários para os E-Cores
                if (profile.BackgroundProcesses.Count > 0 && Topology.ECoreMask > 0)
                {
                    Log($"   📦 Relegando {profile.BackgroundProcesses.Count} processos secundários para E-Cores...");
                    _affinityManager.RelegateBackgroundProcesses(profile.BackgroundProcesses, Topology.ECoreMask);
                    Log($"   ✅ Processos secundários movidos para E-Cores (Mask: 0x{Topology.ECoreMask:X})");
                }
            }
            else
            {
                // ── CPU Homogênea ou perfil sem isolamento: Apenas prioridade ──
                Log("   ℹ CPU homogênea ou isolamento desativado — apenas prioridade será alterada");

                if (_affinityManager.IsolateProcess(pid, Topology.AllCoreMask, priorityClass))
                {
                    Log($"   ✅ Prioridade do jogo definida como {profile.Priority}");
                }
                else
                {
                    Log($"   ⚠ Falha ao definir prioridade do jogo (PID: {pid})");
                }
            }

            // Atualizar estado
            IsActive = true;
            ActiveGameName = profile.GameName;
            ActiveGamePid = pid;

            StatusChanged?.Invoke($"Jogo isolado: {profile.GameName}");
            GameDetected?.Invoke(profile.GameName);

            Log($"   🔒 Isolamento ativo para '{profile.GameName}'");
        }
        catch (Exception ex)
        {
            Log($"   ❌ Erro durante isolamento: {ex.Message}");
            Debug.WriteLine($"[Engine] Exceção no isolamento: {ex}");
        }
    }

    /// <summary>
    /// Callback chamado pelo ProcessWatcher quando um processo é encerrado.
    /// Se o processo corresponder ao jogo ativo, restaura as afinidades originais.
    /// </summary>
    private void OnProcessStopped(string processName, int pid)
    {
        // Verificar se é o jogo ativo que foi encerrado
        if (!IsActive || ActiveGamePid != pid) return;

        var gameName = ActiveGameName ?? processName;
        Log($"🏁 Jogo encerrado: {gameName} (PID: {pid})");
        StatusChanged?.Invoke($"Jogo encerrado: {gameName}");

        try
        {
            // Restaurar afinidades originais se configurado
            if (Settings.RestoreAffinityOnGameClose)
            {
                Log("   🔄 Restaurando afinidades e prioridades originais...");
                _affinityManager.RestoreAllProcesses();
                Log("   ✅ Todas as afinidades restauradas");
            }
            else
            {
                Log("   ℹ Restauração automática desativada — afinidades mantidas");
            }
        }
        catch (Exception ex)
        {
            Log($"   ⚠ Erro ao restaurar afinidades: {ex.Message}");
        }

        // Limpar estado
        IsActive = false;
        ActiveGameName = null;
        ActiveGamePid = null;

        StatusChanged?.Invoke("Monitoramento ativo — aguardando jogo...");
        GameClosed?.Invoke(gameName);

        Log("   Monitoramento retomado. Aguardando próximo jogo...");
    }

    // ═══════════════════════════════════════════════════════
    //                  MÉTODOS AUXILIARES
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Procura um perfil de jogo que corresponda ao nome do processo.
    /// A busca é case-insensitive e ignora perfis desativados.
    /// </summary>
    /// <param name="processName">Nome do executável do processo (ex: "valorant.exe").</param>
    /// <returns>O perfil correspondente, ou null se não encontrado.</returns>
    private GameProfile? FindMatchingProfile(string processName)
    {
        return Settings.Profiles.FirstOrDefault(p =>
            p.IsEnabled &&
            string.Equals(p.ExecutableName, processName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Converte o enum ProcessPriorityLevel para a constante Win32 correspondente.
    /// </summary>
    /// <param name="level">Nível de prioridade do perfil.</param>
    /// <returns>Constante de prioridade Win32 para SetPriorityClass.</returns>
    private static uint GetPriorityClass(ProcessPriorityLevel level)
    {
        return level switch
        {
            ProcessPriorityLevel.Idle => NativeMethods.IDLE_PRIORITY_CLASS,
            ProcessPriorityLevel.BelowNormal => NativeMethods.BELOW_NORMAL_PRIORITY_CLASS,
            ProcessPriorityLevel.Normal => NativeMethods.NORMAL_PRIORITY_CLASS,
            ProcessPriorityLevel.AboveNormal => NativeMethods.ABOVE_NORMAL_PRIORITY_CLASS,
            ProcessPriorityLevel.High => NativeMethods.HIGH_PRIORITY_CLASS,
            _ => NativeMethods.HIGH_PRIORITY_CLASS
        };
    }

    /// <summary>
    /// Emite uma mensagem de log para o evento LogMessage e para o Debug output.
    /// </summary>
    /// <param name="message">Mensagem a ser registrada.</param>
    private void Log(string message)
    {
        Debug.WriteLine($"[Engine] {message}");
        LogMessage?.Invoke(message);
    }

    // ═══════════════════════════════════════════════════════
    //                     DISPOSE
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Libera todos os recursos do engine:
    /// - Para o monitoramento de processos
    /// - Restaura todas as afinidades modificadas
    /// - Desconecta eventos
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Debug.WriteLine("[Engine] Dispose iniciado...");

        try
        {
            // Parar monitoramento
            if (_processWatcher != null)
            {
                _processWatcher.ProcessStarted -= OnProcessStarted;
                _processWatcher.ProcessStopped -= OnProcessStopped;
                _processWatcher.Stop();
                _processWatcher.Dispose();
                _processWatcher = null;
                Debug.WriteLine("[Engine] ProcessWatcher parado e descartado.");
            }

            // Restaurar afinidades se houver jogo ativo
            if (IsActive)
            {
                Debug.WriteLine("[Engine] Restaurando afinidades antes do encerramento...");
                _affinityManager.RestoreAllProcesses();
            }

            IsActive = false;
            ActiveGameName = null;
            ActiveGamePid = null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Engine] Erro durante Dispose: {ex.Message}");
        }

        Debug.WriteLine("[Engine] Dispose concluído.");
    }
}
