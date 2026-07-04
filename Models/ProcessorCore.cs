// -----------------------------------------------------------------------
// ProcessorCore.cs — Modelo de núcleo de processador
// Projeto: CoreIsolator
// -----------------------------------------------------------------------

namespace CoreIsolator.Models;

/// <summary>
/// Tipo do núcleo do processador em arquiteturas híbridas (Intel Alder Lake+).
/// </summary>
public enum CoreType
{
    /// <summary>
    /// Núcleo de desempenho (Performance Core).
    /// Otimizado para cargas de trabalho single-thread e de alta intensidade.
    /// </summary>
    PCore,

    /// <summary>
    /// Núcleo de eficiência (Efficient Core).
    /// Otimizado para tarefas em segundo plano e baixo consumo de energia.
    /// </summary>
    ECore
}

/// <summary>
/// Representa um núcleo físico do processador com suas propriedades de topologia.
/// </summary>
/// <remarks>
/// Cada instância corresponde a um núcleo físico, que pode conter um ou mais
/// processadores lógicos (threads) quando o Hyper-Threading/SMT está habilitado.
/// A máscara de afinidade (<see cref="AffinityMask"/>) é utilizada nas chamadas
/// à API do Windows para fixar processos neste núcleo específico.
/// </remarks>
public record ProcessorCore
{
    /// <summary>
    /// Identificador sequencial do núcleo físico (baseado em zero).
    /// </summary>
    public int CoreId { get; init; }

    /// <summary>
    /// Tipo do núcleo: desempenho (<see cref="CoreType.PCore"/>) ou
    /// eficiência (<see cref="CoreType.ECore"/>).
    /// </summary>
    public CoreType Type { get; init; }

    /// <summary>
    /// Máscara de bits representando os processadores lógicos deste núcleo.
    /// Utilizada diretamente em <c>SetProcessAffinityMask</c> via P/Invoke.
    /// </summary>
    public ulong AffinityMask { get; init; }

    /// <summary>
    /// Classe de eficiência reportada pelo sistema operacional.
    /// Valor 0 indica núcleo de desempenho (P-Core);
    /// valores maiores indicam núcleos de eficiência (E-Core).
    /// </summary>
    public byte EfficiencyClass { get; init; }

    /// <summary>
    /// Índices dos processadores lógicos pertencentes a este núcleo físico.
    /// Em núcleos com SMT/Hyper-Threading, haverá dois ou mais índices.
    /// </summary>
    public int[] LogicalProcessors { get; init; } = [];

    /// <summary>
    /// Indica se o Hyper-Threading (SMT) está habilitado neste núcleo.
    /// Quando <c>true</c>, o núcleo expõe mais de um processador lógico.
    /// </summary>
    public bool HasSmt { get; init; }
}
