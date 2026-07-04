// -----------------------------------------------------------------------
// <copyright file="ProcessWatcher.cs" company="CoreIsolator">
//     Monitor de criação e encerramento de processos via WMI.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics;
using System.Management;

namespace CoreIsolator.Services;

/// <summary>
/// Monitora a criação e o encerramento de processos no sistema operacional
/// utilizando WMI (Windows Management Instrumentation).
/// </summary>
/// <remarks>
/// Utiliza dois observadores WMI:
/// <list type="bullet">
///   <item><description><c>Win32_ProcessStartTrace</c> para detecção de novos processos (requer privilégios de administrador).</description></item>
///   <item><description><c>Win32_ProcessStopTrace</c> para detecção de processos encerrados.</description></item>
/// </list>
/// Caso o <c>Win32_ProcessStartTrace</c> falhe (por falta de privilégios de administrador),
/// é feito um fallback para <c>__InstanceCreationEvent</c> que funciona com privilégios reduzidos.
/// </remarks>
public sealed class ProcessWatcher : IDisposable
{
    /// <summary>
    /// Consulta WMI para rastreamento de criação de processos (requer admin).
    /// </summary>
    private const string StartTraceQuery = "SELECT * FROM Win32_ProcessStartTrace";

    /// <summary>
    /// Consulta WMI para rastreamento de encerramento de processos (requer admin).
    /// </summary>
    private const string StopTraceQuery = "SELECT * FROM Win32_ProcessStopTrace";

    /// <summary>
    /// Consulta WMI alternativa para criação de processos (funciona sem admin).
    /// O intervalo de polling é de 1 segundo.
    /// </summary>
    private const string FallbackStartQuery =
        "SELECT * FROM __InstanceCreationEvent WITHIN 1 WHERE TargetInstance ISA 'Win32_Process'";

    /// <summary>
    /// Consulta WMI alternativa para encerramento de processos (funciona sem admin).
    /// </summary>
    private const string FallbackStopQuery =
        "SELECT * FROM __InstanceDeletionEvent WITHIN 1 WHERE TargetInstance ISA 'Win32_Process'";

    /// <summary>
    /// Observador WMI para criação de processos.
    /// </summary>
    private ManagementEventWatcher? _startWatcher;

    /// <summary>
    /// Observador WMI para encerramento de processos.
    /// </summary>
    private ManagementEventWatcher? _stopWatcher;

    /// <summary>
    /// Indica se o observador está usando o modo de fallback (polling via __InstanceCreationEvent).
    /// </summary>
    private bool _usingFallback;

    /// <summary>
    /// Indica se os observadores estão ativos.
    /// </summary>
    private bool _isRunning;

    /// <summary>
    /// Indica se o objeto foi descartado.
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// Evento disparado quando um novo processo é criado no sistema.
    /// </summary>
    /// <remarks>
    /// Os parâmetros são: nome do processo (string) e PID (int).
    /// </remarks>
    public event Action<string, int>? ProcessStarted;

    /// <summary>
    /// Evento disparado quando um processo é encerrado no sistema.
    /// </summary>
    /// <remarks>
    /// Os parâmetros são: nome do processo (string) e PID (int).
    /// </remarks>
    public event Action<string, int>? ProcessStopped;

    /// <summary>
    /// Indica se os observadores estão ativos e monitorando processos.
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Inicia o monitoramento de processos.
    /// Tenta usar <c>Win32_ProcessStartTrace</c>/<c>Win32_ProcessStopTrace</c> primeiro.
    /// Se falhar (geralmente por falta de privilégios), recorre ao modo de fallback
    /// com <c>__InstanceCreationEvent</c>/<c>__InstanceDeletionEvent</c>.
    /// </summary>
    public void Start()
    {
        if (_isRunning)
        {
            Debug.WriteLine("[ProcessWatcher] Os observadores já estão em execução.");
            return;
        }

        Debug.WriteLine("[ProcessWatcher] Iniciando monitoramento de processos...");

        try
        {
            // Tenta o modo preferencial com Win32_ProcessStartTrace (requer admin)
            _startWatcher = new ManagementEventWatcher(new WqlEventQuery(StartTraceQuery));
            _startWatcher.EventArrived += OnProcessStartTraceEvent;
            _startWatcher.Start();

            _stopWatcher = new ManagementEventWatcher(new WqlEventQuery(StopTraceQuery));
            _stopWatcher.EventArrived += OnProcessStopTraceEvent;
            _stopWatcher.Start();

            _usingFallback = false;
            _isRunning = true;

            Debug.WriteLine("[ProcessWatcher] Monitoramento iniciado com Win32_ProcessStartTrace/StopTrace.");
        }
        catch (ManagementException ex)
        {
            Debug.WriteLine($"[ProcessWatcher] Win32_ProcessStartTrace falhou: {ex.Message}");
            Debug.WriteLine("[ProcessWatcher] Recorrendo ao modo de fallback com __InstanceCreationEvent...");

            // Limpa os observadores que falharam
            CleanupWatchers();
            StartFallbackWatchers();
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"[ProcessWatcher] Acesso não autorizado para Win32_ProcessStartTrace: {ex.Message}");
            Debug.WriteLine("[ProcessWatcher] Recorrendo ao modo de fallback com __InstanceCreationEvent...");

            CleanupWatchers();
            StartFallbackWatchers();
        }
    }

    /// <summary>
    /// Para o monitoramento de processos.
    /// </summary>
    public void Stop()
    {
        if (!_isRunning)
        {
            Debug.WriteLine("[ProcessWatcher] Os observadores já estão parados.");
            return;
        }

        Debug.WriteLine("[ProcessWatcher] Parando monitoramento de processos...");
        CleanupWatchers();
        _isRunning = false;
        Debug.WriteLine("[ProcessWatcher] Monitoramento de processos parado.");
    }

    /// <summary>
    /// Libera todos os recursos utilizados pelo <see cref="ProcessWatcher"/>.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        _disposed = true;

        Debug.WriteLine("[ProcessWatcher] Recursos liberados.");
    }

    /// <summary>
    /// Inicia os observadores no modo de fallback usando polling WMI.
    /// </summary>
    private void StartFallbackWatchers()
    {
        try
        {
            _startWatcher = new ManagementEventWatcher(new WqlEventQuery(FallbackStartQuery));
            _startWatcher.EventArrived += OnFallbackStartEvent;
            _startWatcher.Start();

            _stopWatcher = new ManagementEventWatcher(new WqlEventQuery(FallbackStopQuery));
            _stopWatcher.EventArrived += OnFallbackStopEvent;
            _stopWatcher.Start();

            _usingFallback = true;
            _isRunning = true;

            Debug.WriteLine("[ProcessWatcher] Monitoramento iniciado com __InstanceCreationEvent/__InstanceDeletionEvent (fallback).");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProcessWatcher] Falha crítica ao iniciar observadores de fallback: {ex.Message}");
            CleanupWatchers();
        }
    }

    /// <summary>
    /// Manipulador para o evento <c>Win32_ProcessStartTrace</c>.
    /// Extrai o nome e PID do processo recém-criado.
    /// </summary>
    private void OnProcessStartTraceEvent(object sender, EventArrivedEventArgs e)
    {
        try
        {
            string processName = e.NewEvent.Properties["ProcessName"]?.Value?.ToString() ?? "Desconhecido";
            int pid = Convert.ToInt32(e.NewEvent.Properties["ProcessID"]?.Value ?? 0);

            Debug.WriteLine($"[ProcessWatcher] Processo iniciado (Trace): {processName} (PID: {pid})");
            ProcessStarted?.Invoke(processName, pid);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProcessWatcher] Erro ao processar evento Win32_ProcessStartTrace: {ex.Message}");
        }
    }

    /// <summary>
    /// Manipulador para o evento <c>Win32_ProcessStopTrace</c>.
    /// Extrai o nome e PID do processo encerrado.
    /// </summary>
    private void OnProcessStopTraceEvent(object sender, EventArrivedEventArgs e)
    {
        try
        {
            string processName = e.NewEvent.Properties["ProcessName"]?.Value?.ToString() ?? "Desconhecido";
            int pid = Convert.ToInt32(e.NewEvent.Properties["ProcessID"]?.Value ?? 0);

            Debug.WriteLine($"[ProcessWatcher] Processo encerrado (Trace): {processName} (PID: {pid})");
            ProcessStopped?.Invoke(processName, pid);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProcessWatcher] Erro ao processar evento Win32_ProcessStopTrace: {ex.Message}");
        }
    }

    /// <summary>
    /// Manipulador para o evento de fallback <c>__InstanceCreationEvent</c>.
    /// Extrai o nome e PID do processo a partir da instância <c>Win32_Process</c>.
    /// </summary>
    private void OnFallbackStartEvent(object sender, EventArrivedEventArgs e)
    {
        try
        {
            if (e.NewEvent["TargetInstance"] is ManagementBaseObject targetInstance)
            {
                string processName = targetInstance["Name"]?.ToString() ?? "Desconhecido";
                int pid = Convert.ToInt32(targetInstance["ProcessId"] ?? 0);

                Debug.WriteLine($"[ProcessWatcher] Processo iniciado (Fallback): {processName} (PID: {pid})");
                ProcessStarted?.Invoke(processName, pid);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProcessWatcher] Erro ao processar evento __InstanceCreationEvent: {ex.Message}");
        }
    }

    /// <summary>
    /// Manipulador para o evento de fallback <c>__InstanceDeletionEvent</c>.
    /// Extrai o nome e PID do processo a partir da instância <c>Win32_Process</c>.
    /// </summary>
    private void OnFallbackStopEvent(object sender, EventArrivedEventArgs e)
    {
        try
        {
            if (e.NewEvent["TargetInstance"] is ManagementBaseObject targetInstance)
            {
                string processName = targetInstance["Name"]?.ToString() ?? "Desconhecido";
                int pid = Convert.ToInt32(targetInstance["ProcessId"] ?? 0);

                Debug.WriteLine($"[ProcessWatcher] Processo encerrado (Fallback): {processName} (PID: {pid})");
                ProcessStopped?.Invoke(processName, pid);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProcessWatcher] Erro ao processar evento __InstanceDeletionEvent: {ex.Message}");
        }
    }

    /// <summary>
    /// Limpa e descarta os observadores WMI, removendo os manipuladores de eventos.
    /// </summary>
    private void CleanupWatchers()
    {
        if (_startWatcher is not null)
        {
            try
            {
                _startWatcher.Stop();

                if (_usingFallback)
                    _startWatcher.EventArrived -= OnFallbackStartEvent;
                else
                    _startWatcher.EventArrived -= OnProcessStartTraceEvent;

                _startWatcher.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProcessWatcher] Erro ao limpar observador de início: {ex.Message}");
            }

            _startWatcher = null;
        }

        if (_stopWatcher is not null)
        {
            try
            {
                _stopWatcher.Stop();

                if (_usingFallback)
                    _stopWatcher.EventArrived -= OnFallbackStopEvent;
                else
                    _stopWatcher.EventArrived -= OnProcessStopTraceEvent;

                _stopWatcher.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProcessWatcher] Erro ao limpar observador de parada: {ex.Message}");
            }

            _stopWatcher = null;
        }
    }
}
