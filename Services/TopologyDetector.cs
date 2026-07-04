using System.Diagnostics;
using System.Runtime.InteropServices;
using CoreIsolator.Models;
using CoreIsolator.Native;
using Microsoft.Win32;

namespace CoreIsolator.Services;

public static class TopologyDetector
{
    private const string CpuRegistryKey = @"HARDWARE\DESCRIPTION\System\CentralProcessor\0";
    private const string CpuNameValue = "ProcessorNameString";

    public static CpuTopology Detect()
    {
        Debug.WriteLine("[TopologyDetector] Iniciando detecção de topologia da CPU...");

        string cpuName = GetCpuName();
        var allCores = new List<ProcessorCore>();

        DetectCoresFromApi(allCores);

        if (allCores.Count == 0)
        {
            return CreateFallbackTopology(cpuName);
        }

        bool isHomogeneous = allCores.Select(c => c.EfficiencyClass).Distinct().Count() <= 1;
        var pCores = new List<ProcessorCore>();
        var eCores = new List<ProcessorCore>();

        ulong allCoreMask = 0;
        ulong pCoreMask = 0;
        ulong eCoreMask = 0;
        int totalLogicalProcessors = 0;

        foreach (var core in allCores)
        {
            allCoreMask |= core.AffinityMask;
            totalLogicalProcessors += core.LogicalProcessors.Length;

            if (isHomogeneous)
            {
                pCores.Add(core with { Type = CoreType.PCore });
                pCoreMask |= core.AffinityMask;
            }
            else
            {
                if (core.EfficiencyClass == 0)
                {
                    eCores.Add(core with { Type = CoreType.ECore });
                    eCoreMask |= core.AffinityMask;
                }
                else
                {
                    pCores.Add(core with { Type = CoreType.PCore });
                    pCoreMask |= core.AffinityMask;
                }
            }
        }

        if (!isHomogeneous && eCores.Count == 0)
        {
            pCoreMask = allCoreMask;
            foreach (var core in allCores)
            {
                pCores.Add(core with { Type = CoreType.PCore });
            }
        }

        return new CpuTopology
        {
            CpuName = cpuName,
            AllCores = [.. pCores, .. eCores],
            PCores = pCores,
            ECores = eCores,
            PCoreMask = pCoreMask,
            ECoreMask = eCoreMask,
            AllCoreMask = allCoreMask,
            TotalLogicalProcessors = totalLogicalProcessors,
            TotalPhysicalCores = allCores.Count
        };
    }

    private static void DetectCoresFromApi(List<ProcessorCore> cores)
    {
        uint returnedLength = 0;
        int coreIndex = 0;

        NativeMethods.GetLogicalProcessorInformationEx(
            LOGICAL_PROCESSOR_RELATIONSHIP.RelationProcessorCore,
            IntPtr.Zero,
            ref returnedLength);

        if (returnedLength == 0) return;

        IntPtr buffer = Marshal.AllocHGlobal((int)returnedLength);

        try
        {
            if (!NativeMethods.GetLogicalProcessorInformationEx(
                LOGICAL_PROCESSOR_RELATIONSHIP.RelationProcessorCore,
                buffer,
                ref returnedLength))
            {
                return;
            }

            IntPtr current = buffer;
            uint offset = 0;

            while (offset < returnedLength)
            {
                var relationship = (LOGICAL_PROCESSOR_RELATIONSHIP)Marshal.ReadInt32(current);
                var size = (uint)Marshal.ReadInt32(current + 4);

                if (relationship == LOGICAL_PROCESSOR_RELATIONSHIP.RelationProcessorCore)
                {
                    byte flags = Marshal.ReadByte(current + 8);
                    byte efficiencyClass = Marshal.ReadByte(current + 9);
                    var groupAffinity = Marshal.PtrToStructure<GROUP_AFFINITY>(current + 32);
                    ulong mask = (ulong)(nuint)groupAffinity.Mask;
                    bool hasSmt = (flags & 0x01) != 0;

                    var lps = GetLogicalProcessors(mask);

                    cores.Add(new ProcessorCore
                    {
                        CoreId = coreIndex,
                        EfficiencyClass = efficiencyClass,
                        AffinityMask = mask,
                        HasSmt = hasSmt,
                        LogicalProcessors = lps,
                        Type = CoreType.PCore
                    });
                    coreIndex++;
                }

                offset += size;
                current += (int)size;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static string GetCpuName()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(CpuRegistryKey);
            if (key?.GetValue(CpuNameValue) is string cpuName)
            {
                return cpuName.Trim();
            }
        }
        catch { }
        return "Unknown Processor";
    }

    private static CpuTopology CreateFallbackTopology(string cpuName)
    {
        int processorCount = Environment.ProcessorCount;
        ulong allMask = (1UL << processorCount) - 1;
        var pCores = new List<ProcessorCore>();

        for (int i = 0; i < processorCount; i++)
        {
            pCores.Add(new ProcessorCore
            {
                CoreId = i,
                EfficiencyClass = 1,
                AffinityMask = 1UL << i,
                HasSmt = false,
                LogicalProcessors = [i],
                Type = CoreType.PCore
            });
        }

        return new CpuTopology
        {
            CpuName = cpuName,
            AllCores = pCores,
            PCores = pCores,
            ECores = [],
            PCoreMask = allMask,
            ECoreMask = 0,
            AllCoreMask = allMask,
            TotalLogicalProcessors = processorCount,
            TotalPhysicalCores = processorCount
        };
    }

    private static int[] GetLogicalProcessors(ulong mask)
    {
        var lps = new List<int>();
        for (int i = 0; i < 64; i++)
        {
            if ((mask & (1UL << i)) != 0)
            {
                lps.Add(i);
            }
        }
        return lps.ToArray();
    }
}
