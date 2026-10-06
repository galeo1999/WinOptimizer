namespace WinOptimizer.Models;

public enum MemoryContributorSort { WorkingSet, PrivateMemory, ProcessCount, ApplicationName }

public sealed record MemoryApplicationGroup(
    string ApplicationName,
    string? ExecutablePath,
    IReadOnlyList<ProcessMemoryInfo> Processes,
    ulong? CombinedWorkingSetBytes,
    ulong? CombinedPrivateMemoryBytes)
{
    public int ProcessCount => Processes.Count;
    public int UnavailableProcessCount => Processes.Count(process =>
        !process.WorkingSetBytes.HasValue || !process.PrivateMemoryBytes.HasValue);
    public bool CanOpenLocation => ExecutablePath != null;
    public string MeasurementStatus => UnavailableProcessCount > 0
        ? $"Partial totals · {UnavailableProcessCount:N0} processes have unavailable readings"
        : "Current measurement";
    public string StartupAssociation => string.Join("; ", Processes
        .Select(process => process.StartupAssociation).Distinct(StringComparer.Ordinal));
}
