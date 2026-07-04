// -----------------------------------------------------------------------
// <copyright file="Advapi32.cs" company="CoreIsolator">
//     Declarações P/Invoke para advapi32.dll
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.InteropServices;

namespace CoreIsolator.Native;

/// <summary>
/// Contém as declarações P/Invoke para funções exportadas pela advapi32.dll.
/// Inclui operações de tokens de segurança e gerenciamento de privilégios.
/// </summary>
internal static partial class NativeMethods
{
    // -----------------------------------------------------------------------
    //  Constantes de acesso a tokens
    // -----------------------------------------------------------------------

    /// <summary>
    /// Direito de acesso necessário para ajustar os privilégios de um token de segurança.
    /// </summary>
    internal const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;

    /// <summary>
    /// Direito de acesso necessário para consultar informações de um token de segurança.
    /// </summary>
    internal const uint TOKEN_QUERY = 0x0008;

    // -----------------------------------------------------------------------
    //  Constantes de atributos de privilégio
    // -----------------------------------------------------------------------

    /// <summary>
    /// Atributo que indica que o privilégio está habilitado.
    /// Usado no campo <see cref="LUID_AND_ATTRIBUTES.Attributes"/>.
    /// </summary>
    internal const uint SE_PRIVILEGE_ENABLED = 0x00000002;

    // -----------------------------------------------------------------------
    //  Nomes de privilégios conhecidos
    // -----------------------------------------------------------------------

    /// <summary>
    /// Nome do privilégio de depuração. Quando habilitado, permite abrir handles
    /// para qualquer processo do sistema, independentemente do descritor de segurança.
    /// Essencial para manipular processos de outros usuários ou processos protegidos.
    /// </summary>
    internal const string SE_DEBUG_NAME = "SeDebugPrivilege";

    // -----------------------------------------------------------------------
    //  Funções de token de segurança
    // -----------------------------------------------------------------------

    /// <summary>
    /// Abre o token de acesso associado a um processo.
    /// </summary>
    /// <param name="processHandle">
    /// Handle do processo cujo token será aberto. O processo deve ter sido aberto
    /// com o direito <c>PROCESS_QUERY_INFORMATION</c> ou equivalente.
    /// </param>
    /// <param name="desiredAccess">
    /// Máscara de direitos de acesso desejados para o token
    /// (ex.: <see cref="TOKEN_ADJUST_PRIVILEGES"/> | <see cref="TOKEN_QUERY"/>).
    /// </param>
    /// <param name="tokenHandle">
    /// Recebe o handle do token aberto. Deve ser fechado com
    /// <see cref="NativeMethods.CloseHandle"/> quando não for mais necessário.
    /// </param>
    /// <returns>
    /// <see langword="true"/> se a função for bem-sucedida;
    /// <see langword="false"/> caso contrário.
    /// </returns>
    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool OpenProcessToken(
        IntPtr processHandle,
        uint desiredAccess,
        out IntPtr tokenHandle);

    /// <summary>
    /// Consulta o valor LUID (Locally Unique Identifier) de um privilégio
    /// pelo seu nome no sistema especificado.
    /// </summary>
    /// <param name="lpSystemName">
    /// Nome do sistema onde o privilégio será consultado.
    /// Use <see langword="null"/> para o sistema local.
    /// </param>
    /// <param name="lpName">
    /// Nome do privilégio (ex.: <see cref="SE_DEBUG_NAME"/>).
    /// </param>
    /// <param name="lpLuid">
    /// Recebe o <see cref="LUID"/> que identifica o privilégio no sistema.
    /// </param>
    /// <returns>
    /// <see langword="true"/> se a função for bem-sucedida;
    /// <see langword="false"/> caso contrário.
    /// </returns>
    [LibraryImport("advapi32.dll", EntryPoint = "LookupPrivilegeValueW",
        SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool LookupPrivilegeValue(
        [MarshalAs(UnmanagedType.LPWStr)] string? lpSystemName,
        [MarshalAs(UnmanagedType.LPWStr)] string lpName,
        out LUID lpLuid);

    /// <summary>
    /// Habilita ou desabilita privilégios no token de acesso especificado.
    /// Para habilitar o <see cref="SE_DEBUG_NAME"/>, preencha uma estrutura
    /// <see cref="TOKEN_PRIVILEGES"/> com o LUID obtido via
    /// <see cref="LookupPrivilegeValue"/> e o atributo <see cref="SE_PRIVILEGE_ENABLED"/>.
    /// </summary>
    /// <param name="tokenHandle">
    /// Handle do token de acesso (deve ter <see cref="TOKEN_ADJUST_PRIVILEGES"/>).
    /// </param>
    /// <param name="disableAllPrivileges">
    /// Se <see langword="true"/>, desabilita todos os privilégios e ignora
    /// <paramref name="newState"/>.
    /// </param>
    /// <param name="newState">
    /// Referência para a estrutura <see cref="TOKEN_PRIVILEGES"/> que especifica
    /// os privilégios e seus novos estados.
    /// </param>
    /// <param name="bufferLength">
    /// Tamanho em bytes do buffer <paramref name="previousState"/>.
    /// Passe <c>0</c> se não precisar do estado anterior.
    /// </param>
    /// <param name="previousState">
    /// Ponteiro para um buffer que receberá o estado anterior dos privilégios.
    /// Use <see cref="IntPtr.Zero"/> se não for necessário.
    /// </param>
    /// <param name="returnLength">
    /// Ponteiro para receber o tamanho real necessário para <paramref name="previousState"/>.
    /// Use <see cref="IntPtr.Zero"/> se não for necessário.
    /// </param>
    /// <returns>
    /// <see langword="true"/> se a função for bem-sucedida;
    /// <see langword="false"/> caso contrário. Mesmo retornando <see langword="true"/>,
    /// verifique <see cref="Marshal.GetLastWin32Error"/> — o erro
    /// <c>ERROR_NOT_ALL_ASSIGNED (1300)</c> indica que nem todos os privilégios
    /// foram ajustados.
    /// </returns>
    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool AdjustTokenPrivileges(
        IntPtr tokenHandle,
        [MarshalAs(UnmanagedType.Bool)] bool disableAllPrivileges,
        ref TOKEN_PRIVILEGES newState,
        uint bufferLength,
        IntPtr previousState,
        IntPtr returnLength);
}
