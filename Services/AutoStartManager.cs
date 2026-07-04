using System.Diagnostics;
using System.IO;
using Microsoft.Win32.TaskScheduler;

namespace CoreIsolator.Services;

/// <summary>
/// Gerencia a inicialização automática do CoreIsolator com o Windows
/// usando o Agendador de Tarefas (Task Scheduler).
/// 
/// O uso do Task Scheduler em vez da chave de registro HKCU\...\Run
/// permite que o app inicie com privilégios elevados (Administrador)
/// silenciosamente, sem exibir o popup de UAC toda vez que o PC ligar.
/// </summary>
public static class AutoStartManager
{
    /// <summary>
    /// Nome da tarefa no Agendador de Tarefas do Windows.
    /// </summary>
    private const string TaskName = "CoreIsolator";

    /// <summary>
    /// Descrição da tarefa agendada.
    /// </summary>
    private const string TaskDescription = "Inicia o CoreIsolator automaticamente com o Windows para gerenciar a afinidade de núcleos da CPU.";

    /// <summary>
    /// Verifica se a tarefa de auto-start já existe no Agendador de Tarefas.
    /// </summary>
    /// <returns>True se a tarefa existe e está habilitada.</returns>
    public static bool IsAutoStartEnabled()
    {
        try
        {
            using var taskService = new TaskService();
            var task = taskService.GetTask(TaskName);
            return task?.Enabled ?? false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AutoStartManager] Erro ao verificar auto-start: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Cria uma tarefa no Agendador de Tarefas do Windows para iniciar
    /// o CoreIsolator automaticamente no logon do usuário com privilégios
    /// elevados (sem popup de UAC).
    /// </summary>
    public static void EnableAutoStart()
    {
        try
        {
            var executablePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(executablePath))
            {
                Debug.WriteLine("[AutoStartManager] Não foi possível obter o caminho do executável.");
                throw new InvalidOperationException("Caminho do executável não disponível.");
            }

            using var taskService = new TaskService();

            // Remover tarefa existente, se houver, para recriá-la limpa
            var existingTask = taskService.GetTask(TaskName);
            if (existingTask != null)
            {
                taskService.RootFolder.DeleteTask(TaskName, false);
                Debug.WriteLine("[AutoStartManager] Tarefa existente removida para recriação.");
            }

            // Criar nova definição de tarefa
            var taskDefinition = taskService.NewTask();
            taskDefinition.RegistrationInfo.Description = TaskDescription;
            taskDefinition.RegistrationInfo.Author = "CoreIsolator";

            // Configurar para executar com privilégios mais altos (Administrador)
            // Isso evita o popup de UAC na inicialização do Windows
            taskDefinition.Principal.RunLevel = TaskRunLevel.Highest;
            taskDefinition.Principal.LogonType = TaskLogonType.InteractiveToken;

            // Trigger: ao fazer logon no Windows
            taskDefinition.Triggers.Add(new LogonTrigger
            {
                Enabled = true,
                // Atraso de 5 segundos para garantir que o desktop carregou
                Delay = TimeSpan.FromSeconds(5)
            });

            // Ação: executar o CoreIsolator
            taskDefinition.Actions.Add(new ExecAction(
                executablePath,
                arguments: null,
                workingDirectory: Path.GetDirectoryName(executablePath)
            ));

            // Configurações da tarefa
            taskDefinition.Settings.AllowDemandStart = true;
            taskDefinition.Settings.DisallowStartIfOnBatteries = false;
            taskDefinition.Settings.StopIfGoingOnBatteries = false;
            taskDefinition.Settings.ExecutionTimeLimit = TimeSpan.Zero; // Sem limite de execução
            taskDefinition.Settings.AllowHardTerminate = false;
            taskDefinition.Settings.StartWhenAvailable = true;
            taskDefinition.Settings.Enabled = true;

            // Não reiniciar em caso de falha (o usuário deve reiniciar manualmente)
            taskDefinition.Settings.RestartCount = 0;

            // Registrar a tarefa
            taskService.RootFolder.RegisterTaskDefinition(
                TaskName,
                taskDefinition,
                TaskCreation.CreateOrUpdate,
                null, // Usa o usuário atual
                null, // Sem senha (token interativo)
                TaskLogonType.InteractiveToken
            );

            Debug.WriteLine($"[AutoStartManager] Tarefa '{TaskName}' criada com sucesso no Agendador de Tarefas.");
            Debug.WriteLine($"[AutoStartManager] Executável: {executablePath}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AutoStartManager] Erro ao criar tarefa de auto-start: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Remove a tarefa de auto-start do Agendador de Tarefas do Windows.
    /// </summary>
    public static void DisableAutoStart()
    {
        try
        {
            using var taskService = new TaskService();
            var task = taskService.GetTask(TaskName);

            if (task != null)
            {
                taskService.RootFolder.DeleteTask(TaskName, false);
                Debug.WriteLine($"[AutoStartManager] Tarefa '{TaskName}' removida do Agendador de Tarefas.");
            }
            else
            {
                Debug.WriteLine($"[AutoStartManager] Tarefa '{TaskName}' não encontrada — nada para remover.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AutoStartManager] Erro ao remover tarefa de auto-start: {ex.Message}");
            throw;
        }
    }
}
