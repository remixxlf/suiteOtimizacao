// -----------------------------------------------------------------------
// <copyright file="PrivilegeManager.cs" company="CoreIsolator">
//     Gerenciador de privilégios do processo atual.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using CoreIsolator.Native;

namespace CoreIsolator.Services;

/// <summary>
/// Classe estática responsável por elevar os privilégios do processo atual,
/// habilitando o SeDebugPrivilege necessário para manipular processos de outros usuários.
/// </summary>
public static class PrivilegeManager
{
    /// <summary>
    /// Nome do privilégio de depuração do Windows.
    /// </summary>
    private const string SeDebugPrivilege = "SeDebugPrivilege";

    /// <summary>
    /// Código de erro retornado quando nem todos os privilégios foram atribuídos.
    /// </summary>
    private const int ERROR_NOT_ALL_ASSIGNED = 1300;

    /// <summary>
    /// Flag para habilitar um privilégio no token de acesso.
    /// </summary>
    private const uint SE_PRIVILEGE_ENABLED = 0x00000002;

    /// <summary>
    /// Acesso necessário para ajustar privilégios do token.
    /// </summary>
    private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;

    /// <summary>
    /// Acesso necessário para consultar informações do token.
    /// </summary>
    private const uint TOKEN_QUERY = 0x0008;

    // -- Modificação (DIDÁTICA): Verificação de Segurança (Administrador) --
    // Este método verifica preventivamente se o usuário atual executou a aplicação com
    // privilégios elevados (Executar como Administrador).
    public static bool IsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PrivilegeManager] Erro ao verificar status de administrador: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Habilita o privilégio SeDebugPrivilege no processo atual.
    /// </summary>
    public static bool EnableDebugPrivilege()
    {
        // 1. Validação Preventiva
        if (!IsAdministrator())
        {
            Debug.WriteLine("[PrivilegeManager] O processo não possui privilégios de Administrador. Abortando elevação.");
            return false;
        }

        IntPtr tokenHandle = IntPtr.Zero;

        try
        {
            // 2. Chamadas nativas (P/Invoke) protegidas em try/catch
            // Obtém o handle do processo atual
            IntPtr processHandle = NativeMethods.GetCurrentProcess();

            // Abre o token de acesso do processo
            if (!NativeMethods.OpenProcessToken(processHandle, TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out tokenHandle))
            {
                int erro = Marshal.GetLastWin32Error();
                Debug.WriteLine($"[PrivilegeManager] Falha ao abrir token do processo. Erro Win32: {erro}");
                return false;
            }

            Debug.WriteLine("[PrivilegeManager] Token do processo aberto com sucesso.");

            // Consulta o LUID (Locally Unique Identifier) do privilégio SeDebugPrivilege
            if (!NativeMethods.LookupPrivilegeValue(null, SeDebugPrivilege, out LUID luid))
            {
                int erro = Marshal.GetLastWin32Error();
                Debug.WriteLine($"[PrivilegeManager] Falha ao consultar LUID do SeDebugPrivilege. Erro Win32: {erro}");
                return false;
            }

            Debug.WriteLine($"[PrivilegeManager] LUID do SeDebugPrivilege obtido: LowPart={luid.LowPart}, HighPart={luid.HighPart}");

            // Monta a estrutura TOKEN_PRIVILEGES com o privilégio a ser habilitado
            var tokenPrivileges = new TOKEN_PRIVILEGES
            {
                PrivilegeCount = 1,
                Privileges = new LUID_AND_ATTRIBUTES
                {
                    Luid = luid,
                    Attributes = SE_PRIVILEGE_ENABLED
                }
            };

            // Ajusta os privilégios do token
            if (!NativeMethods.AdjustTokenPrivileges(
                tokenHandle,
                false,
                ref tokenPrivileges,
                0,
                IntPtr.Zero,
                IntPtr.Zero))
            {
                int erro = Marshal.GetLastWin32Error();
                Debug.WriteLine($"[PrivilegeManager] Falha ao ajustar privilégios do token. Erro Win32: {erro}");
                return false;
            }

            // Verifica se todos os privilégios foram atribuídos
            int ultimoErro = Marshal.GetLastWin32Error();
            if (ultimoErro == ERROR_NOT_ALL_ASSIGNED)
            {
                Debug.WriteLine("[PrivilegeManager] Nem todos os privilégios foram atribuídos. " +
                                "O processo pode não estar sendo executado como administrador.");
                return false;
            }

            Debug.WriteLine("[PrivilegeManager] SeDebugPrivilege habilitado com sucesso.");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PrivilegeManager] Exceção ao habilitar SeDebugPrivilege: {ex.Message}");
            return false;
        }
        finally
        {
            // Sempre fecha o handle do token para evitar vazamento de recursos
            if (tokenHandle != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(tokenHandle);
                Debug.WriteLine("[PrivilegeManager] Handle do token fechado.");
            }
        }
    }
}
