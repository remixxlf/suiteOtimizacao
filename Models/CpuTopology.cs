// -----------------------------------------------------------------------
// CpuTopology.cs — Modelo de topologia da CPU
// Projeto: CoreIsolator
// -----------------------------------------------------------------------

namespace CoreIsolator.Models;

/// <summary>
/// Representa a topologia completa do processador, incluindo a classificação
/// dos núcleos em P-Cores e E-Cores, suas máscaras de afinidade agregadas
/// e informações gerais do hardware.
/// </summary>
/// <remarks>
/// Esta classe é preenchida pelo serviço de detecção de topologia ao iniciar
/// a aplicação. As máscaras agregadas (<see cref="PCoreMask"/>, <see cref="ECoreMask"/>)
/// são calculadas com OR bit-a-bit de todos os núcleos de cada categoria,
/// permitindo definir afinidade de processo para grupos inteiros de núcleos
/// em uma única chamada à API do Windows.
/// </remarks>
public class CpuTopology
{
    /// <summary>
    /// Lista de todos os núcleos físicos detectados no sistema.
    /// </summary>
    public List<ProcessorCore> AllCores { get; set; } = [];

    /// <summary>
    /// Lista dos núcleos de desempenho (Performance Cores).
    /// Vazia em processadores que não possuem arquitetura híbrida.
    /// </summary>
    public List<ProcessorCore> PCores { get; set; } = [];

    /// <summary>
    /// Lista dos núcleos de eficiência (Efficient Cores).
    /// Vazia em processadores que não possuem arquitetura híbrida.
    /// </summary>
    public List<ProcessorCore> ECores { get; set; } = [];

    /// <summary>
    /// Máscara de afinidade combinada de todos os P-Cores.
    /// Resultado do OR bit-a-bit de <see cref="ProcessorCore.AffinityMask"/>
    /// de cada núcleo em <see cref="PCores"/>.
    /// </summary>
    public ulong PCoreMask { get; set; }

    /// <summary>
    /// Máscara de afinidade combinada de todos os E-Cores.
    /// Resultado do OR bit-a-bit de <see cref="ProcessorCore.AffinityMask"/>
    /// de cada núcleo em <see cref="ECores"/>.
    /// </summary>
    public ulong ECoreMask { get; set; }

    /// <summary>
    /// Máscara de afinidade combinada de todos os núcleos do sistema.
    /// Equivalente a <c><see cref="PCoreMask"/> | <see cref="ECoreMask"/></c>.
    /// </summary>
    public ulong AllCoreMask { get; set; }

    /// <summary>
    /// Indica se o processador possui arquitetura híbrida (P-Cores + E-Cores).
    /// Retorna <c>true</c> quando ambos os tipos de núcleo estão presentes,
    /// como nos processadores Intel de 12ª geração em diante.
    /// </summary>
    public bool IsHybrid => ECores.Count > 0 && PCores.Count > 0;

    /// <summary>
    /// Nome comercial do processador (ex: "13th Gen Intel Core i7-13700K").
    /// Obtido a partir do registro do Windows ou via instrução CPUID.
    /// </summary>
    public string CpuName { get; set; } = "Unknown";

    /// <summary>
    /// Número total de processadores lógicos (threads) disponíveis no sistema.
    /// Inclui threads de Hyper-Threading/SMT quando habilitado.
    /// </summary>
    public int TotalLogicalProcessors { get; set; }

    /// <summary>
    /// Número total de núcleos físicos detectados no sistema.
    /// </summary>
    public int TotalPhysicalCores { get; set; }
}
