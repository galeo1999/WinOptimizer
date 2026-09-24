namespace WinOptimizer.Models;

public class ProcessMemoryInfo
{
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = "";
    public ulong? WorkingSetBytes { get; set; }
    public ulong? PrivateMemoryBytes { get; set; }
    // Windows PagefileUsage is private commit, not measured bytes resident on disk.
    public ulong? PagedMemoryBytes { get; set; }
    public string? ExecutablePath { get; set; }
    // Indicates memory counters were read; the executable path may still be unavailable.
    public bool IsAccessible { get; set; }
    public string StartupAssociation { get; set; } = "No confirmed match";
    public string AccessStatus => IsAccessible ? "Available" : "Unavailable / exited";
}
