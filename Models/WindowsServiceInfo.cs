namespace WinOptimizer.Models;

public sealed record WindowsServiceInfo
{
    public string ServiceName { get; init; } = "Unknown";
    public string DisplayName { get; init; } = "Unknown";
    public string Status { get; init; } = "Unknown";
    public string StartType { get; init; } = "Unknown";
    public int? ProcessId { get; init; }
    // WMI PathName is a command line, not necessarily a safely resolved executable.
    public string? ConfiguredCommand { get; init; }
    public string? ExecutablePath { get; init; }
    // Per-service resource attribution is unavailable, including for shared hosts.
    public ulong? WorkingSetBytes => null;
    public ulong? PrivateMemoryBytes => null;
    public double? CpuUsagePercent => null;
    public bool IsWindowsService => true; // Registered service, not a publisher classification.
    public bool? IsMicrosoftService => null;
    public string Origin => "Unknown";
    public string? Description { get; init; }
    public bool IsAccessible { get; init; }
    public string AccessStatus => IsAccessible ? "Metadata available" : "Metadata incomplete / inaccessible";
    public string Details => $"{ServiceName} · {Status} · Startup: {StartType} · Origin: {Origin}";
}
