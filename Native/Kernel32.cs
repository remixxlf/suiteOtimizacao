// -----------------------------------------------------------------------
// <copyright file="Kernel32.cs" company="CoreIsolator">
//     Declarações P/Invoke para kernel32.dll
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.InteropServices;

namespace CoreIsolator.Native;

/// <summary>
/// Contém as declarações P/Invoke para funções exportadas pela kernel32.dll.
/// Inclui operações de topologia de CPU, afinidade de processo e classe de prioridade.
/// </summary>
internal static partial class NativeMethods
{
    // -----------------------------------------------------------------------
    //  Constantes de direitos de acesso a processos
    // -----------------------------------------------------------------------

    /// <summary>
    /// Direito de acesso necessário para definir informações do processo,
    /// como afinidade e classe de prioridade.
    /// </summary>
    internal const uint PROCESS_SET_INFORMATION = 0x0200;

    /// <summary>
    /// Direito de acesso necessário para consultar informações completas do processo.
    /// </summary>
    internal const uint PROCESS_QUERY_INFORMATION = 0x0400;

    /// <summary>
    /// Direito de acesso para consultar informações limitadas do processo.
    /// Funciona mesmo com processos protegidos (Protected Process Light).
    /// </summary>
    internal const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    // -----------------------------------------------------------------------
    //  Constantes de classe de prioridade
    // -----------------------------------------------------------------------

    /// <summary>
    /// Classe de prioridade ociosa. Threads só executam quando o sistema está ocioso.
    /// </summary>
    internal const uint IDLE_PRIORITY_CLASS = 0x00000040;

    /// <summary>
    /// Classe de prioridade abaixo do normal.
    /// </summary>
    internal const uint BELOW_NORMAL_PRIORITY_CLASS = 0x00004000;

    /// <summary>
    /// Classe de prioridade normal (padrão para a maioria dos processos).
    /// </summary>
    internal const uint NORMAL_PRIORITY_CLASS = 0x00000020;

    /// <summary>
    /// Classe de prioridade acima do normal.
    /// </summary>
    internal const uint ABOVE_NORMAL_PRIORITY_CLASS = 0x00008000;

    /// <summary>
    /// Classe de prioridade alta. Usar com cautela — pode afetar a responsividade do sistema.
    /// </summary>
    internal const uint HIGH_PRIORITY_CLASS = 0x00000080;

    /// <summary>
    /// Classe de prioridade em tempo real. Requer privilégios elevados.
    /// <b>CUIDADO:</b> pode tornar o sistema completamente irresponsivo se usado indevidamente.
    /// </summary>
    internal const uint REALTIME_PRIORITY_CLASS = 0x00000100;

    // -----------------------------------------------------------------------
    //  Funções de topologia de CPU
    // -----------------------------------------------------------------------

    /// <summary>
    /// Obtém informações estendidas sobre a topologia do processador lógico do sistema.
    /// A função preenche um buffer com estruturas <see cref="SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX"/>
    /// de tamanho variável.
    /// </summary>
    /// <param name="relationshipType">
    /// Tipo de relacionamento a ser consultado (ex.: núcleos, pacotes, caches, NUMA).
    /// Use <see cref="LOGICAL_PROCESSOR_RELATIONSHIP.RelationAll"/> para obter tudo.
    /// </param>
    /// <param name="buffer">
    /// Ponteiro para o buffer que receberá os dados. Pode ser <see cref="IntPtr.Zero"/>
    /// na primeira chamada para descobrir o tamanho necessário.
    /// </param>
    /// <param name="returnedLength">
    /// Na entrada, o tamanho do buffer em bytes. Na saída, o tamanho necessário ou utilizado.
    /// </param>
    /// <returns>
    /// <see langword="true"/> se a função for bem-sucedida;
    /// <see langword="false"/> caso contrário (verifique <see cref="Marshal.GetLastWin32Error"/>).
    /// </returns>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetLogicalProcessorInformationEx(
        LOGICAL_PROCESSOR_RELATIONSHIP relationshipType,
        IntPtr buffer,
        ref uint returnedLength);

    /// <summary>
    /// Obtém informações sobre os conjuntos de CPUs (CPU Sets) disponíveis no sistema.
    /// API mais simples que <see cref="GetLogicalProcessorInformationEx"/> para
    /// cenários de isolamento de núcleos.
    /// </summary>
    /// <param name="information">
    /// Ponteiro para o buffer que receberá as estruturas
    /// <see cref="SYSTEM_CPU_SET_INFORMATION"/>. Pode ser <see cref="IntPtr.Zero"/>
    /// para descobrir o tamanho necessário.
    /// </param>
    /// <param name="bufferLength">Tamanho do buffer em bytes.</param>
    /// <param name="returnedLength">Tamanho necessário ou efetivamente escrito no buffer, em bytes.</param>
    /// <param name="process">
    /// Handle do processo para o qual a informação será consultada.
    /// Use <see cref="IntPtr.Zero"/> para informações de todo o sistema.
    /// </param>
    /// <param name="flags">Reservado. Deve ser <c>0</c>.</param>
    /// <returns>
    /// <see langword="true"/> se a função for bem-sucedida;
    /// <see langword="false"/> caso contrário.
    /// </returns>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetSystemCpuSetInformation(
        IntPtr information,
        uint bufferLength,
        out uint returnedLength,
        IntPtr process,
        uint flags);

    // -----------------------------------------------------------------------
    //  Funções de afinidade de processo
    // -----------------------------------------------------------------------

    /// <summary>
    /// Define a máscara de afinidade do processador para o processo especificado.
    /// Cada bit ligado na máscara representa um processador lógico no qual o processo
    /// poderá executar.
    /// </summary>
    /// <param name="hProcess">Handle do processo (deve ter <see cref="PROCESS_SET_INFORMATION"/>).</param>
    /// <param name="dwProcessAffinityMask">
    /// Máscara de afinidade desejada. Deve ser um subconjunto da máscara de afinidade do sistema.
    /// </param>
    /// <returns>
    /// <see langword="true"/> se a função for bem-sucedida;
    /// <see langword="false"/> caso contrário.
    /// </returns>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetProcessAffinityMask(
        IntPtr hProcess,
        UIntPtr dwProcessAffinityMask);

    /// <summary>
    /// Consulta a máscara de afinidade atual do processo e a máscara de afinidade do sistema.
    /// Útil para salvar a afinidade original antes de modificá-la.
    /// </summary>
    /// <param name="hProcess">Handle do processo (deve ter <see cref="PROCESS_QUERY_INFORMATION"/> ou
    /// <see cref="PROCESS_QUERY_LIMITED_INFORMATION"/>).</param>
    /// <param name="lpProcessAffinityMask">Recebe a máscara de afinidade atual do processo.</param>
    /// <param name="lpSystemAffinityMask">Recebe a máscara de afinidade do sistema.</param>
    /// <returns>
    /// <see langword="true"/> se a função for bem-sucedida;
    /// <see langword="false"/> caso contrário.
    /// </returns>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetProcessAffinityMask(
        IntPtr hProcess,
        out UIntPtr lpProcessAffinityMask,
        out UIntPtr lpSystemAffinityMask);

    // -----------------------------------------------------------------------
    //  Funções de classe de prioridade
    // -----------------------------------------------------------------------

    /// <summary>
    /// Define a classe de prioridade do processo especificado.
    /// </summary>
    /// <param name="hProcess">Handle do processo (deve ter <see cref="PROCESS_SET_INFORMATION"/>).</param>
    /// <param name="dwPriorityClass">
    /// Nova classe de prioridade. Use uma das constantes como
    /// <see cref="NORMAL_PRIORITY_CLASS"/>, <see cref="HIGH_PRIORITY_CLASS"/>, etc.
    /// </param>
    /// <returns>
    /// <see langword="true"/> se a função for bem-sucedida;
    /// <see langword="false"/> caso contrário.
    /// </returns>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetPriorityClass(
        IntPtr hProcess,
        uint dwPriorityClass);

    /// <summary>
    /// Consulta a classe de prioridade do processo especificado.
    /// </summary>
    /// <param name="hProcess">Handle do processo.</param>
    /// <returns>
    /// A classe de prioridade do processo, ou <c>0</c> em caso de falha.
    /// </returns>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial uint GetPriorityClass(IntPtr hProcess);

    // -----------------------------------------------------------------------
    //  Funções de gerenciamento de handles de processo
    // -----------------------------------------------------------------------

    /// <summary>
    /// Abre um handle para um processo existente, dado o seu identificador (PID).
    /// </summary>
    /// <param name="dwDesiredAccess">
    /// Máscara de direitos de acesso desejados (ex.: <see cref="PROCESS_SET_INFORMATION"/>
    /// | <see cref="PROCESS_QUERY_INFORMATION"/>).
    /// </param>
    /// <param name="bInheritHandle">
    /// Se <see langword="true"/>, o handle pode ser herdado por processos filhos.
    /// </param>
    /// <param name="dwProcessId">Identificador do processo (PID).</param>
    /// <returns>
    /// Handle aberto para o processo, ou <see cref="IntPtr.Zero"/> em caso de falha.
    /// </returns>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial IntPtr OpenProcess(
        uint dwDesiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle,
        int dwProcessId);

    /// <summary>
    /// Fecha um handle de objeto do kernel (processo, thread, token, etc.).
    /// </summary>
    /// <param name="hObject">Handle a ser fechado.</param>
    /// <returns>
    /// <see langword="true"/> se o handle foi fechado com sucesso;
    /// <see langword="false"/> caso contrário.
    /// </returns>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CloseHandle(IntPtr hObject);

    /// <summary>
    /// Retorna um pseudo-handle para o processo atual.
    /// Este handle não precisa ser fechado com <see cref="CloseHandle"/>.
    /// </summary>
    /// <returns>Pseudo-handle do processo atual (valor constante <c>-1</c>).</returns>
    [LibraryImport("kernel32.dll")]
    internal static partial IntPtr GetCurrentProcess();
}
