using System.ComponentModel;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using WinOptimizer.Models;

namespace WinOptimizer.Services;

public enum BackgroundFilter { All, RunningServices, AutomaticServices, ManualServices }
public enum BackgroundSort { Memory, Name, StartupType, ProcessCount }

public sealed class BackgroundActivityAnalyzer
{
    public Task<BackgroundAnalysisResult> AnalyzeAsync(IReadOnlyList<StartupItem>? startup = null,
        CancellationToken cancellationToken = default)
    {
        var startupSnapshot = (startup ?? []).Select(s => new StartupItem
        { Name = s.Name, Command = s.Command, State = s.State, SourceType = s.SourceType }).ToArray();
        return Task.Run(async () =>
        {
            var (services, warning) = ReadServices(cancellationToken);
            MemoryAnalysisResult memory = await new MemoryAnalyzer().AnalyzeAsync(startupSnapshot, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var windows = new Dictionary<int, bool?>();
            foreach (var item in memory.Processes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using Process process = Process.GetProcessById(item.ProcessId);
                    windows[item.ProcessId] = process.MainWindowHandle != IntPtr.Zero;
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or ArgumentException or NotSupportedException)
                { windows[item.ProcessId] = null; }
            }
            return BuildResult(services, memory.Processes, windows, memory.CapturedAt, warning);
        });
    }

    private static (List<WindowsServiceInfo>, string?) ReadServices(CancellationToken token)
    {
        var services = new List<WindowsServiceInfo>();
        string? warning = null;
        try
        {
            token.ThrowIfCancellationRequested();
            using var searcher = new ManagementObjectSearcher("root\\cimv2",
                "SELECT Name, DisplayName, State, StartMode, ProcessId, PathName, Description FROM Win32_Service",
                new EnumerationOptions { ReturnImmediately = true, Rewindable = false, Timeout = TimeSpan.FromSeconds(5) });
            using var results = searcher.Get();
            foreach (ManagementObject service in results)
            {
                using (service)
                {
                    token.ThrowIfCancellationRequested();
                    bool accessible = true;
                    object? Read(string property)
                    {
                        try { return service[property]; }
                        catch (Exception ex) when (IsUnavailable(ex))
                        {
                            accessible = false;
                            warning = "Some service metadata was inaccessible; results are incomplete.";
                            return null;
                        }
                    }
                    uint? pid = Read("ProcessId") as uint?;
                    string? name = Read("Name") as string;
                    string? display = Read("DisplayName") as string;
                    string? state = Read("State") as string;
                    string? mode = Read("StartMode") as string;
                    string? command = Read("PathName") as string;
                    string? description = Read("Description") as string;
                    services.Add(new WindowsServiceInfo
                    {
                        ServiceName = name ?? "Unknown", DisplayName = display ?? name ?? "Unknown",
                        Status = state ?? "Unknown", StartType = mode ?? "Unknown",
                        ProcessId = pid is > 0 and <= int.MaxValue ? (int)pid.Value : null,
                        ConfiguredCommand = command, Description = description, IsAccessible = accessible
                    });
                }
            }
        }
        catch (Exception ex) when (IsUnavailable(ex))
        { warning = $"Service enumeration incomplete: {ex.Message}"; }
        token.ThrowIfCancellationRequested();
        return (services, warning);
    }

    private static bool IsUnavailable(Exception ex) => ex is ManagementException or UnauthorizedAccessException or COMException;

    // One resource-bearing row per PID. Services without a sampled host carry no memory.
    public static BackgroundAnalysisResult BuildResult(IReadOnlyList<WindowsServiceInfo> services,
        IReadOnlyList<ProcessMemoryInfo> processes, IReadOnlyDictionary<int, bool?> mainWindows,
        DateTimeOffset capturedAt, string? warning = null)
    {
        var processIndex = processes.DistinctBy(p => p.ProcessId).ToDictionary(p => p.ProcessId);
        services = services.Select(s => s with
        {
            ExecutablePath = s.ProcessId.HasValue && processIndex.TryGetValue(s.ProcessId.Value, out var host)
                ? host.ExecutablePath : null
        }).ToArray();
        var byPid = services.Where(s => s.ProcessId > 0).GroupBy(s => s.ProcessId!.Value)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<WindowsServiceInfo>)g.ToArray());
        var rows = new List<BackgroundProcessInfo>();
        var seen = new HashSet<int>();
        foreach (var process in processes)
        {
            if (!seen.Add(process.ProcessId)) continue;
            byPid.TryGetValue(process.ProcessId, out var hosted);
            mainWindows.TryGetValue(process.ProcessId, out bool? hasWindow);
            if (hosted == null && hasWindow == true) continue;
            rows.Add(new BackgroundProcessInfo { Process = process, Services = hosted ?? [], HasMainWindow = hasWindow });
        }
        foreach (var service in services.Where(s => !s.ProcessId.HasValue || !seen.Contains(s.ProcessId.Value)))
            rows.Add(new BackgroundProcessInfo { Services = [service] });
        return new BackgroundAnalysisResult(rows, services, capturedAt, warning);
    }

    public static IReadOnlyList<BackgroundProcessInfo> FilterAndSort(BackgroundAnalysisResult? result,
        BackgroundFilter filter, BackgroundSort sort)
    {
        IEnumerable<BackgroundProcessInfo> rows = result?.Groups ?? [];
        rows = filter switch
        {
            BackgroundFilter.RunningServices => rows.Where(r => r.Services.Any(s => s.Status == "Running")),
            BackgroundFilter.AutomaticServices => rows.Where(r => r.Services.Any(s => s.StartType == "Auto")),
            BackgroundFilter.ManualServices => rows.Where(r => r.Services.Any(s => s.StartType == "Manual")),
            _ => rows
        };
        return (sort switch
        {
            BackgroundSort.Name => rows.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase),
            BackgroundSort.StartupType => rows.OrderBy(r => r.StartupType, StringComparer.OrdinalIgnoreCase),
            BackgroundSort.ProcessCount => rows.OrderByDescending(r => r.ProcessCount),
            _ => rows.OrderByDescending(r => r.WorkingSetBytes)
        }).ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
