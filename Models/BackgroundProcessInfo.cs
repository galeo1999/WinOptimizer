namespace WinOptimizer.Models;

public sealed class BackgroundProcessInfo
{
    public ProcessMemoryInfo? Process { get; init; }
    public IReadOnlyList<WindowsServiceInfo> Services { get; init; } = [];
    public bool? HasMainWindow { get; init; }
    public string Name => Services.Count == 1 ? Services[0].DisplayName
        : Process?.ProcessName ?? "Unavailable host";
    public int ProcessCount => Process == null ? 0 : 1;
    public ulong? WorkingSetBytes => Process?.WorkingSetBytes;
    public ulong? PrivateMemoryBytes => Process?.PrivateMemoryBytes;
    public string StartupType => Services.Count == 0 ? "Not a service" :
        string.Join(", ", Services.Select(s => s.StartType).Distinct().Order());
    public string ServiceState => Services.Count == 0 ? "No service association"
        : string.Join(", ", Services.Select(s => s.Status).Distinct().Order());
    public string Association => Services.Count > 0
        ? $"{Services.Count} hosted service(s) · " + (Services.Count > 1 ? "Shared host" : "Host-level memory")
        : HasMainWindow == false ? "Background candidate: no main window detected" : "Window status unknown";
    public string AccessStatus => Process == null ? "No measured host (stopped, exited or inaccessible)"
        : Process.IsAccessible ? "Memory counters available; origin unknown" : "Memory unavailable / process exited";
}
