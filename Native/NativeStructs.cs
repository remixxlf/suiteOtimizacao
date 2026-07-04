// -----------------------------------------------------------------------
// <copyright file="NativeStructs.cs" company="CoreIsolator">
//     Estruturas e enumerações nativas do Win32 para interoperabilidade.
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.InteropServices;

namespace CoreIsolator.Native;

// =========================================================================
//  Estruturas de Token e Privilégios
// =========================================================================

/// <summary>
/// Identificador Localmente Único (LUID). Garantido como único apenas até a
/// próxima reinicialização do sistema. Usado para identificar privilégios de segurança.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct LUID
{
    /// <summary>Parte baixa do identificador (32 bits menos significativos).</summary>
    public uint LowPart;

    /// <summary>Parte alta do identificador (32 bits mais significativos).</summary>
    public int HighPart;
}

/// <summary>
/// Representa um LUID junto com seus atributos (ex.: habilitado, desabilitado).
/// Usado dentro de <see cref="TOKEN_PRIVILEGES"/> para especificar o estado
/// de cada privilégio.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct LUID_AND_ATTRIBUTES
{
    /// <summary>O identificador LUID do privilégio.</summary>
    public LUID Luid;

    /// <summary>
    /// Atributos do privilégio. Use <see cref="NativeMethods.SE_PRIVILEGE_ENABLED"/>
    /// para habilitar o privilégio.
    /// </summary>
    public uint Attributes;
}

/// <summary>
/// Contém informações sobre um conjunto de privilégios.
/// Esta versão define um único elemento em <see cref="Privileges"/>,
/// suficiente para operações que ajustam um privilégio por vez
/// (o cenário mais comum, como habilitar <c>SeDebugPrivilege</c>).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct TOKEN_PRIVILEGES
{
    /// <summary>Número de privilégios no array <see cref="Privileges"/>.</summary>
    public uint PrivilegeCount;

    /// <summary>
    /// Privilégio único. Para múltiplos privilégios simultâneos, seria necessário
    /// alocar memória não gerenciada com espaço para elementos adicionais.
    /// </summary>
    public LUID_AND_ATTRIBUTES Privileges;
}

// =========================================================================
//  Enumeração de Relacionamento de Processador Lógico
// =========================================================================

/// <summary>
/// Define os tipos de relacionamento entre processadores lógicos que podem
/// ser consultados via <see cref="NativeMethods.GetLogicalProcessorInformationEx"/>.
/// </summary>
internal enum LOGICAL_PROCESSOR_RELATIONSHIP : uint
{
    /// <summary>Relacionamento entre processadores lógicos de um mesmo núcleo físico.</summary>
    RelationProcessorCore = 0,

    /// <summary>Relacionamento de nó NUMA (Non-Uniform Memory Access).</summary>
    RelationNumaNode = 1,

    /// <summary>Relacionamento de cache (L1, L2, L3, etc.).</summary>
    RelationCache = 2,

    /// <summary>Relacionamento entre processadores lógicos de um mesmo pacote físico (socket).</summary>
    RelationProcessorPackage = 3,

    /// <summary>Relacionamento de grupo de processadores (para sistemas com mais de 64 processadores lógicos).</summary>
    RelationGroup = 4,

    /// <summary>
    /// Solicita todos os tipos de relacionamento.
    /// Válido apenas como parâmetro de consulta; nunca retornado como resultado.
    /// </summary>
    RelationAll = 0xFFFF
}

// =========================================================================
//  Estruturas de Topologia do Processador
// =========================================================================

/// <summary>
/// Cabeçalho de uma entrada retornada por
/// <see cref="NativeMethods.GetLogicalProcessorInformationEx"/>.
/// Cada entrada tem tamanho variável; o campo <see cref="Size"/> indica
/// o tamanho total (incluindo dados que seguem este cabeçalho).
/// </summary>
/// <remarks>
/// O corpo da estrutura varia conforme o valor de <see cref="Relationship"/>.
/// Para <see cref="LOGICAL_PROCESSOR_RELATIONSHIP.RelationProcessorCore"/>,
/// os dados que seguem são do tipo <see cref="PROCESSOR_RELATIONSHIP"/>.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal struct SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX
{
    /// <summary>Tipo de relacionamento descrito por esta entrada.</summary>
    public LOGICAL_PROCESSOR_RELATIONSHIP Relationship;

    /// <summary>Tamanho total desta entrada em bytes, incluindo dados de tamanho variável.</summary>
    public uint Size;
}

/// <summary>
/// Descreve o relacionamento de um processador (núcleo ou pacote).
/// Segue o cabeçalho <see cref="SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX"/>
/// quando o <see cref="SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX.Relationship"/>
/// é <see cref="LOGICAL_PROCESSOR_RELATIONSHIP.RelationProcessorCore"/> ou
/// <see cref="LOGICAL_PROCESSOR_RELATIONSHIP.RelationProcessorPackage"/>.
/// </summary>
/// <remarks>
/// Após esta estrutura, seguem <see cref="GroupCount"/> elementos
/// <see cref="GROUP_AFFINITY"/> em memória contígua, que devem ser lidos
/// manualmente via ponteiro.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal struct PROCESSOR_RELATIONSHIP
{
    /// <summary>
    /// Flags do processador. Para núcleos, o bit 0 (<c>LTP_PC_SMT</c>)
    /// indica se o núcleo possui Hyper-Threading / SMT habilitado.
    /// </summary>
    public byte Flags;

    /// <summary>
    /// Classe de eficiência do processador (0 = desempenho, valores maiores = eficiência).
    /// Disponível a partir do Windows 10; em versões anteriores será <c>0</c>.
    /// Útil para distinguir P-cores de E-cores em arquiteturas híbridas (ex.: Intel Alder Lake).
    /// </summary>
    public byte EfficiencyClass;

    /// <summary>Bytes reservados pelo sistema. Não utilizar.</summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
    public byte[] Reserved;

    /// <summary>
    /// Número de grupos de processadores associados a este relacionamento.
    /// Determina quantas estruturas <see cref="GROUP_AFFINITY"/> seguem esta
    /// estrutura em memória.
    /// </summary>
    public ushort GroupCount;

    // NOTA: Após este campo, seguem GroupCount elementos GROUP_AFFINITY
    // de tamanho variável. Eles devem ser lidos via aritmética de ponteiro.
}

/// <summary>
/// Especifica um grupo de processadores e uma máscara de afinidade dentro desse grupo.
/// Em sistemas com até 64 processadores lógicos, normalmente há apenas o grupo 0.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct GROUP_AFFINITY
{
    /// <summary>
    /// Máscara de bits indicando quais processadores lógicos dentro do grupo
    /// estão associados ao relacionamento. Cada bit corresponde a um processador lógico.
    /// </summary>
    public UIntPtr Mask;

    /// <summary>Número do grupo de processadores (geralmente 0 em sistemas comuns).</summary>
    public ushort Group;

    /// <summary>Valores reservados pelo sistema. Não utilizar.</summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public ushort[] Reserved;
}

// =========================================================================
//  Estrutura de Informação de CPU Set
// =========================================================================

/// <summary>
/// Contém informações sobre um conjunto de CPUs (CPU Set) do sistema,
/// retornado por <see cref="NativeMethods.GetSystemCpuSetInformation"/>.
/// Cada instância descreve um processador lógico individual com seus
/// atributos de topologia e escalonamento.
/// </summary>
/// <remarks>
/// Esta estrutura é uma versão simplificada (campos achatados) da união
/// original do Windows SDK, que usa uma <c>union</c> interna chamada
/// <c>CpuSet</c>. Os campos aqui representam a interpretação mais comum
/// (<c>CpuSet</c>) da união.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal struct SYSTEM_CPU_SET_INFORMATION
{
    /// <summary>Tamanho total desta estrutura em bytes.</summary>
    public uint Size;

    /// <summary>
    /// Tipo de informação. Atualmente, o único valor definido é
    /// <c>CpuSetInformation (0)</c>.
    /// </summary>
    public uint Type;

    // --- Início dos campos da união CpuSet ---

    /// <summary>
    /// Identificador numérico único do CPU Set.
    /// Pode ser usado com <c>SetProcessDefaultCpuSets</c> e APIs relacionadas.
    /// </summary>
    public uint Id;

    /// <summary>
    /// Grupo de processadores ao qual este processador lógico pertence.
    /// </summary>
    public ushort Group;

    /// <summary>
    /// Índice do processador lógico dentro do grupo.
    /// Corresponde à posição do bit na máscara de afinidade.
    /// </summary>
    public byte LogicalProcessorIndex;

    /// <summary>
    /// Índice do núcleo físico ao qual este processador lógico pertence.
    /// Processadores lógicos com o mesmo <see cref="CoreIndex"/> compartilham
    /// o mesmo núcleo físico (via Hyper-Threading / SMT).
    /// </summary>
    public byte CoreIndex;

    /// <summary>
    /// Índice do cache de último nível (L3 na maioria das CPUs modernas)
    /// compartilhado por este processador lógico.
    /// </summary>
    public byte LastLevelCacheIndex;

    /// <summary>
    /// Índice do nó NUMA ao qual este processador lógico está associado.
    /// </summary>
    public byte NumaNodeIndex;

    /// <summary>
    /// Classe de eficiência do processador. Em arquiteturas híbridas
    /// (ex.: Intel Alder Lake), valores diferentes distinguem
    /// P-cores (desempenho) de E-cores (eficiência).
    /// <c>0</c> tipicamente indica o núcleo de maior desempenho.
    /// </summary>
    public byte EfficiencyClass;

    /// <summary>
    /// Byte de flags combinadas. Contém informações como:
    /// <list type="bullet">
    ///   <item><description>Bit 0: <c>Parked</c> — se o núcleo está estacionado.</description></item>
    ///   <item><description>Bit 1: <c>Allocated</c> — se o CPU Set está alocado a algum processo.</description></item>
    ///   <item><description>Bit 2: <c>AllocatedToTargetProcess</c> — se está alocado ao processo de consulta.</description></item>
    ///   <item><description>Bit 3: <c>RealTime</c> — se é um CPU Set em tempo real.</description></item>
    /// </list>
    /// </summary>
    public byte AllFlags;

    /// <summary>
    /// Classe de escalonamento garantida pelo sistema para este CPU Set.
    /// Valores maiores indicam maior prioridade de escalonamento.
    /// </summary>
    public uint SchedulingClass;

    /// <summary>
    /// Tag de alocação associada ao CPU Set via
    /// <c>SetProcessDefaultCpuSetMasks</c> ou APIs equivalentes.
    /// Valor <c>0</c> indica que nenhuma tag foi definida.
    /// </summary>
    public ulong AllocationTag;
}
