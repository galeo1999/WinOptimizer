using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using WinOptimizer.Models;

namespace WinOptimizer.Services;

public sealed class MemoryAnalyzer
{
    public Task<MemoryAnalysisResult> AnalyzeAsync(
        IReadOnlyList<StartupItem>? startupItems = null,
        CancellationToken cancellationToken = default)
    {
        // Copy the informational fields so startup UI changes cannot affect an in-flight scan.
        var startup = (startupItems ?? []).Select(item =>
            new StartupItem { Name = item.Name, Command = item.Command, State = item.State,
                SourceType = item.SourceType }).ToArray();
        return Task.Run(() => Analyze(startup, cancellationToken), cancellationToken);
    }

    private static MemoryAnalysisResult Analyze(StartupItem[] startup, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        MemorySnapshot snapshot = new SystemAnalyzer().GetMemorySnapshot();
        if (GetPhysicallyInstalledSystemMemory(out ulong installedKb))
            snapshot.InstalledBytes = installedKb * 1024;

        uint size = (uint)Marshal.SizeOf<PerformanceInformation>();
        if (GetPerformanceInfo(out PerformanceInformation performance, size))
        {
            ulong pageSize = performance.PageSize.ToUInt64();
            snapshot.CommitUsageBytes = performance.CommitTotal.ToUInt64() * pageSize;
            snapshot.CommitLimitBytes = performance.CommitLimit.ToUInt64() * pageSize;
            snapshot.CachedBytes = performance.SystemCache.ToUInt64() * pageSize;
        }

        token.ThrowIfCancellationRequested();
        ulong pageFileTotal = 0, pageFileUsed = 0;
        PageFileCallback callback = (IntPtr context, ref PageFileInformation info, string name) =>
        {
            // Do not throw managed cancellation exceptions across a native callback boundary.
            if (token.IsCancellationRequested)
                return false;
            pageFileTotal += info.TotalSize.ToUInt64() * (ulong)Environment.SystemPageSize;
            pageFileUsed += info.TotalInUse.ToUInt64() * (ulong)Environment.SystemPageSize;
            return true;
        };
        bool pageFilesRead = EnumPageFiles(callback, IntPtr.Zero);
        token.ThrowIfCancellationRequested();
        if (pageFilesRead)
        {
            // A successful enumeration with no page files really does mean zero.
            snapshot.PageFileTotalBytes = pageFileTotal;
            snapshot.PageFileUsedBytes = pageFileUsed;
        }

        var associations = StartupProcessMatcher.CreateIndex(startup);
        var results = new List<ProcessMemoryInfo>();
        Process[] processes = Process.GetProcesses();
        try
        {
            foreach (Process process in processes)
            {
                token.ThrowIfCancellationRequested();
                var item = new ProcessMemoryInfo { ProcessId = process.Id, ProcessName = $"PID {process.Id}" };
                try
                {
                    item.ProcessName = process.ProcessName;
                }
                catch (Exception ex) when (IsProcessUnavailable(ex)) { }

                // Query rights only: no VM_READ, VM_WRITE, SET_QUOTA, TERMINATE, or debug privilege.
                using SafeProcessHandle handle = OpenProcess(0x1000, false, process.Id);
                if (!handle.IsInvalid)
                {
                    uint counterSize = (uint)Marshal.SizeOf<ProcessMemoryCounters>();
                    if (GetProcessMemoryInfo(handle, out ProcessMemoryCounters counters, counterSize))
                    {
                        item.WorkingSetBytes = counters.WorkingSetSize.ToUInt64();
                        item.PrivateMemoryBytes = counters.PrivateUsage.ToUInt64();
                        item.PagedMemoryBytes = counters.PagefileUsage.ToUInt64();
                        item.IsAccessible = true;
                    }
                    var path = new StringBuilder(32768);
                    uint capacity = (uint)path.Capacity;
                    if (QueryFullProcessImageName(handle, 0, path, ref capacity))
                    {
                        item.ExecutablePath = path.ToString();
                        item.StartupAssociation = StartupProcessMatcher.Describe(
                            StartupProcessMatcher.FindMatches(item.ExecutablePath, associations));
                    }
                }
                results.Add(item);
            }
        }
        finally
        {
            // Dispose every Process even if the scan is cancelled halfway through enumeration.
            foreach (Process process in processes)
                process.Dispose();
        }
        token.ThrowIfCancellationRequested();
        return new MemoryAnalysisResult(snapshot, results
            .OrderByDescending(item => item.WorkingSetBytes)
            .ThenBy(item => item.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ProcessId).ToArray(), DateTimeOffset.Now);
    }

    private static bool IsProcessUnavailable(Exception exception) =>
        exception is Win32Exception or InvalidOperationException or NotSupportedException;

    [StructLayout(LayoutKind.Sequential)]
    private struct PerformanceInformation
    {
        public uint Size;
        public UIntPtr CommitTotal, CommitLimit, CommitPeak, PhysicalTotal, PhysicalAvailable,
            SystemCache, KernelTotal, KernelPaged, KernelNonpaged, PageSize;
        public uint HandleCount, ProcessCount, ThreadCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessMemoryCounters
    {
        public uint Size, PageFaultCount;
        public UIntPtr PeakWorkingSetSize, WorkingSetSize, QuotaPeakPagedPoolUsage, QuotaPagedPoolUsage,
            QuotaPeakNonPagedPoolUsage, QuotaNonPagedPoolUsage, PagefileUsage, PeakPagefileUsage, PrivateUsage;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PageFileInformation
    {
        public uint Size, Reserved;
        public UIntPtr TotalSize, TotalInUse, PeakUsage;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private delegate bool PageFileCallback(IntPtr context, ref PageFileInformation info,
        [MarshalAs(UnmanagedType.LPWStr)] string name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPhysicallyInstalledSystemMemory(out ulong totalMemoryInKilobytes);

    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPerformanceInfo(out PerformanceInformation info, uint size);

    [DllImport("psapi.dll", EntryPoint = "EnumPageFilesW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumPageFiles(PageFileCallback callback, IntPtr context);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint access,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int processId);

    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessMemoryInfo(SafeProcessHandle process, out ProcessMemoryCounters info, uint size);

    [DllImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags,
        StringBuilder path, ref uint size);
}
