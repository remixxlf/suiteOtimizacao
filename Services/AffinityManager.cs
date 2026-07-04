// -----------------------------------------------------------------------
// <copyright file="AffinityManager.cs" company="CoreIsolator">
//     Gerenciador de afinidade e prioridade de processos.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;
using CoreIsolator.Native;

namespace CoreIsolator.Services;

/// <summary>
/// Gerencia a afinidade de CPU e a prioridade de processos no Windows.
/// Permite isolar processos em núcleos específicos (P-Cores ou E-Cores)
/// e restaurar as configurações originais posteriormente.
/// </summary>
/// <remarks>
/// Mantém um dicionário interno com os estados originais de todos os processos
/// modificados, permitindo a restauração completa quando necessário.
/// Processos críticos do sistema são protegidos por uma lista negra (blacklist)
/// e nunca são modificados.
/// </remarks>
public sealed class AffinityManager
{
    /// <summary>
    /// Flag de acesso para definir informações do processo.
    /// </summary>
    private const uint PROCESS_SET_INFORMATION = 0x0200;

    /// <summary>
    /// Flag de acesso para consultar informações do processo.
    /// </summary>
    private const uint PROCESS_QUERY_INFORMATION = 0x0400;

    /// <summary>
    /// Classe de prioridade "Abaixo do Normal" para processos em segundo plano.
    /// </summary>
    public const uint BELOW_NORMAL_PRIORITY_CLASS = 0x00004000;

    /// <summary>
    /// Classe de prioridade "Normal".
    /// </summary>
    public const uint NORMAL_PRIORITY_CLASS = 0x00000020;

    /// <summary>
    /// Classe de prioridade "Alta" para processos prioritários (jogos).
    /// </summary>
    public const uint HIGH_PRIORITY_CLASS = 0x00000080;

    /// <summary>
    /// Lista de processos críticos do sistema que NUNCA devem ter afinidade ou prioridade alteradas.
    /// Modificar esses processos pode causar instabilidade, tela azul ou travamento completo do sistema.
    /// </summary>
    private static readonly HashSet<string> _criticalProcessBlacklist = new(StringComparer.OrdinalIgnoreCase)
    {
        "csrss.exe",
        "smss.exe",
        "winlogon.exe",
        "explorer.exe",
        "dwm.exe",
        "lsass.exe",
        "services.exe",
        "svchost.exe",
        "wininit.exe",
        "System",
        "System Idle Process",
        "Registry"
    };

    /// <summary>
    /// Dicionário que armazena o estado original (máscara de afinidade e classe de prioridade)
    /// de cada processo modificado, indexado pelo PID.
    /// </summary>
    private readonly Dictionary<int, (ulong OriginalMask, uint OriginalPriority)> _savedStates = new();

    /// <summary>
    /// Objeto de sincronização para acesso thread-safe ao dicionário de estados salvos.
    /// </summary>
    private readonly object _lock = new();

    /// <summary>
    /// Obtém o número de processos que possuem estados salvos para restauração.
    /// </summary>
    public int SavedStateCount
    {
        get
        {
            lock (_lock)
            {
                return _savedStates.Count;
            }
        }
    }

    /// <summary>
    /// Verifica se um nome de processo está na lista negra de processos críticos do sistema.
    /// </summary>
    /// <param name="processName">Nome do processo a verificar (com ou sem extensão .exe).</param>
    /// <returns><c>true</c> se o processo é crítico e não deve ser modificado.</returns>
    public static bool IsCriticalProcess(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
            return true;

        // Verifica tanto com quanto sem a extensão .exe
        return _criticalProcessBlacklist.Contains(processName) ||
               _criticalProcessBlacklist.Contains(processName + ".exe") ||
               _criticalProcessBlacklist.Contains(Path.GetFileNameWithoutExtension(processName));
    }

    /// <summary>
    /// Isola um processo em um conjunto específico de núcleos de CPU, definindo
    /// sua máscara de afinidade e classe de prioridade.
    /// </summary>
    /// <param name="pid">ID do processo (PID) a ser isolado.</param>
    /// <param name="mask">Máscara de afinidade de CPU desejada (bitmask dos processadores lógicos).</param>
    /// <param name="priorityClass">Classe de prioridade desejada (ex.: HIGH_PRIORITY_CLASS).</param>
    /// <returns>
    /// <c>true</c> se o processo foi isolado com sucesso;
    /// <c>false</c> se houve falha ao abrir o processo ou aplicar as configurações.
    /// </returns>
    public bool IsolateProcess(int pid, ulong mask, uint priorityClass)
    {
        IntPtr processHandle = IntPtr.Zero;

        try
        {
            // Abre o handle do processo com as permissões necessárias
            processHandle = NativeMethods.OpenProcess(
                PROCESS_SET_INFORMATION | PROCESS_QUERY_INFORMATION,
                false,
                pid);

            if (processHandle == IntPtr.Zero)
            {
                int erro = Marshal.GetLastWin32Error();
                Debug.WriteLine($"[AffinityManager] Falha ao abrir processo PID {pid}. Erro Win32: {erro}");
                return false;
            }

            // Salva o estado original antes de modificar
            if (!SaveOriginalState(pid, processHandle))
            {
                Debug.WriteLine($"[AffinityManager] Falha ao salvar estado original do processo PID {pid}.");
                return false;
            }

            // Aplica a nova máscara de afinidade
            if (!NativeMethods.SetProcessAffinityMask(processHandle, (UIntPtr)mask))
            {
                int erro = Marshal.GetLastWin32Error();
                Debug.WriteLine($"[AffinityManager] Falha ao definir afinidade do processo PID {pid}. " +
                                $"Mask=0x{mask:X16}. Erro Win32: {erro}");
                return false;
            }

            // Aplica a nova classe de prioridade
            if (!NativeMethods.SetPriorityClass(processHandle, priorityClass))
            {
                int erro = Marshal.GetLastWin32Error();
                Debug.WriteLine($"[AffinityManager] Falha ao definir prioridade do processo PID {pid}. " +
                                $"Prioridade=0x{priorityClass:X8}. Erro Win32: {erro}");
                return false;
            }

            Debug.WriteLine($"[AffinityManager] Processo PID {pid} isolado com sucesso. " +
                            $"Mask=0x{mask:X16}, Prioridade=0x{priorityClass:X8}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AffinityManager] Exceção ao isolar processo PID {pid}: {ex.Message}");
            return false;
        }
        finally
        {
            if (processHandle != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(processHandle);
            }
        }
    }

    /// <summary>
    /// Relega processos em segundo plano para os núcleos de eficiência (E-Cores),
    /// definindo prioridade abaixo do normal.
    /// </summary>
    /// <param name="processNames">Nomes dos processos a serem relegados (sem extensão .exe).</param>
    /// <param name="eCoreMask">Máscara de afinidade dos núcleos de eficiência (E-Cores).</param>
    /// <returns>
    /// <c>true</c> se pelo menos um processo foi relegado com sucesso;
    /// <c>false</c> se nenhum processo foi encontrado ou todos falharam.
    /// </returns>
    public bool RelegateBackgroundProcesses(IEnumerable<string> processNames, ulong eCoreMask)
    {
        bool algumSucesso = false;

        foreach (string name in processNames)
        {
            // Verifica se o processo está na lista negra
            if (IsCriticalProcess(name))
            {
                Debug.WriteLine($"[AffinityManager] Processo '{name}' está na lista negra. Ignorando.");
                continue;
            }

            try
            {
                // Remove a extensão .exe se presente para usar com GetProcessesByName
                string cleanName = Path.GetFileNameWithoutExtension(name);
                Process[] processes = Process.GetProcessesByName(cleanName);

                if (processes.Length == 0)
                {
                    Debug.WriteLine($"[AffinityManager] Nenhum processo encontrado com o nome '{cleanName}'.");
                    continue;
                }

                foreach (Process process in processes)
                {
                    try
                    {
                        int pid = process.Id;

                        // Verificação dupla: nome completo do processo contra a blacklist
                        string fullName = process.ProcessName;
                        if (IsCriticalProcess(fullName))
                        {
                            Debug.WriteLine($"[AffinityManager] Processo '{fullName}' (PID: {pid}) é crítico. Ignorando.");
                            continue;
                        }

                        bool resultado = IsolateProcess(pid, eCoreMask, BELOW_NORMAL_PRIORITY_CLASS);
                        if (resultado)
                        {
                            Debug.WriteLine($"[AffinityManager] Processo '{fullName}' (PID: {pid}) relegado para E-Cores.");
                            algumSucesso = true;
                        }
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AffinityManager] Erro ao relegar processo '{name}': {ex.Message}");
            }
        }

        Debug.WriteLine($"[AffinityManager] Relegação de processos em segundo plano concluída. " +
                        $"Sucesso: {algumSucesso}");
        return algumSucesso;
    }

    /// <summary>
    /// Restaura a afinidade de CPU e prioridade originais de todos os processos
    /// que foram modificados anteriormente.
    /// </summary>
    /// <remarks>
    /// Processos que já foram encerrados são automaticamente removidos da lista de estados salvos.
    /// </remarks>
    public void RestoreAllProcesses()
    {
        Debug.WriteLine("[AffinityManager] Restaurando todos os processos modificados...");

        List<int> pidsParaRemover = new();
        KeyValuePair<int, (ulong OriginalMask, uint OriginalPriority)>[] snapshot;

        lock (_lock)
        {
            snapshot = _savedStates.ToArray();
        }

        foreach (var kvp in snapshot)
        {
            int pid = kvp.Key;
            var (originalMask, originalPriority) = kvp.Value;

            if (!RestoreProcessInternal(pid, originalMask, originalPriority))
            {
                // Processo provavelmente já foi encerrado; marca para remoção
                pidsParaRemover.Add(pid);
            }
        }

        lock (_lock)
        {
            // Remove entradas de processos que falharam na restauração (provavelmente encerrados)
            foreach (int pid in pidsParaRemover)
            {
                _savedStates.Remove(pid);
                Debug.WriteLine($"[AffinityManager] Estado salvo removido para PID {pid} (processo possivelmente encerrado).");
            }

            // Remove também os que foram restaurados com sucesso
            foreach (var kvp in snapshot)
            {
                if (!pidsParaRemover.Contains(kvp.Key))
                {
                    _savedStates.Remove(kvp.Key);
                }
            }
        }

        Debug.WriteLine($"[AffinityManager] Restauração concluída. {pidsParaRemover.Count} processos falharam (possivelmente encerrados).");
    }

    /// <summary>
    /// Restaura a afinidade de CPU e prioridade originais de um único processo.
    /// </summary>
    /// <param name="pid">ID do processo (PID) a ser restaurado.</param>
    public void RestoreProcess(int pid)
    {
        (ulong OriginalMask, uint OriginalPriority) state;

        lock (_lock)
        {
            if (!_savedStates.TryGetValue(pid, out state))
            {
                Debug.WriteLine($"[AffinityManager] Nenhum estado salvo encontrado para o processo PID {pid}.");
                return;
            }
        }

        bool sucesso = RestoreProcessInternal(pid, state.OriginalMask, state.OriginalPriority);

        lock (_lock)
        {
            _savedStates.Remove(pid);
        }

        if (sucesso)
        {
            Debug.WriteLine($"[AffinityManager] Processo PID {pid} restaurado com sucesso.");
        }
        else
        {
            Debug.WriteLine($"[AffinityManager] Falha ao restaurar processo PID {pid} (possivelmente encerrado).");
        }
    }

    /// <summary>
    /// Salva o estado original (afinidade e prioridade) de um processo antes de modificá-lo.
    /// </summary>
    /// <param name="pid">ID do processo.</param>
    /// <param name="processHandle">Handle aberto do processo com permissões de consulta.</param>
    /// <returns><c>true</c> se o estado foi salvo com sucesso.</returns>
    private bool SaveOriginalState(int pid, IntPtr processHandle)
    {
        lock (_lock)
        {
            // Se já temos o estado salvo, não sobrescreve (preserva o estado original verdadeiro)
            if (_savedStates.ContainsKey(pid))
            {
                Debug.WriteLine($"[AffinityManager] Estado original já salvo para PID {pid}. Mantendo o existente.");
                return true;
            }
        }

        // Obtém a máscara de afinidade atual do processo
        if (!NativeMethods.GetProcessAffinityMask(processHandle, out UIntPtr processAffinityMask, out UIntPtr systemAffinityMask))
        {
            int erro = Marshal.GetLastWin32Error();
            Debug.WriteLine($"[AffinityManager] Falha ao obter afinidade do processo PID {pid}. Erro Win32: {erro}");
            return false;
        }

        // Obtém a classe de prioridade atual do processo
        uint priorityClass = NativeMethods.GetPriorityClass(processHandle);
        if (priorityClass == 0)
        {
            int erro = Marshal.GetLastWin32Error();
            Debug.WriteLine($"[AffinityManager] Falha ao obter prioridade do processo PID {pid}. Erro Win32: {erro}");
            return false;
        }

        ulong originalMask = (ulong)(nuint)processAffinityMask;

        lock (_lock)
        {
            _savedStates[pid] = (originalMask, priorityClass);
        }

        Debug.WriteLine($"[AffinityManager] Estado original salvo para PID {pid}: " +
                        $"Mask=0x{originalMask:X16}, Prioridade=0x{priorityClass:X8}");
        return true;
    }

    /// <summary>
    /// Implementação interna de restauração de um processo individual.
    /// </summary>
    /// <param name="pid">ID do processo.</param>
    /// <param name="originalMask">Máscara de afinidade original.</param>
    /// <param name="originalPriority">Classe de prioridade original.</param>
    /// <returns><c>true</c> se a restauração foi bem-sucedida.</returns>
    private static bool RestoreProcessInternal(int pid, ulong originalMask, uint originalPriority)
    {
        IntPtr processHandle = IntPtr.Zero;

        try
        {
            processHandle = NativeMethods.OpenProcess(
                PROCESS_SET_INFORMATION | PROCESS_QUERY_INFORMATION,
                false,
                pid);

            if (processHandle == IntPtr.Zero)
            {
                int erro = Marshal.GetLastWin32Error();
                Debug.WriteLine($"[AffinityManager] Falha ao abrir processo PID {pid} para restauração. Erro Win32: {erro}");
                return false;
            }

            bool affinityOk = NativeMethods.SetProcessAffinityMask(processHandle, (UIntPtr)originalMask);
            if (!affinityOk)
            {
                int erro = Marshal.GetLastWin32Error();
                Debug.WriteLine($"[AffinityManager] Falha ao restaurar afinidade do PID {pid}. Erro Win32: {erro}");
            }

            bool priorityOk = NativeMethods.SetPriorityClass(processHandle, originalPriority);
            if (!priorityOk)
            {
                int erro = Marshal.GetLastWin32Error();
                Debug.WriteLine($"[AffinityManager] Falha ao restaurar prioridade do PID {pid}. Erro Win32: {erro}");
            }

            Debug.WriteLine($"[AffinityManager] Processo PID {pid} restaurado: " +
                            $"Afinidade={affinityOk}, Prioridade={priorityOk}");

            return affinityOk && priorityOk;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AffinityManager] Exceção ao restaurar processo PID {pid}: {ex.Message}");
            return false;
        }
        finally
        {
            if (processHandle != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(processHandle);
            }
        }
    }
}
