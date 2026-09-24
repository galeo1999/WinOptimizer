namespace WinOptimizer.Models;

public class MemorySnapshot
{
    // WinOptimizer presentation guidance, not a Windows-defined critical boundary.
    public const double CommitWarningThresholdPercent = 90.0;

    public ulong TotalBytes { get; set; }

    public ulong AvailableBytes { get; set; }

    public ulong UsedBytes { get; set; }

    public uint UsagePercentage { get; set; }

    public ulong? InstalledBytes { get; set; }
    public ulong? CommitUsageBytes { get; set; }
    public ulong? CommitLimitBytes { get; set; }
    // GetPerformanceInfo.SystemCache: standby list plus system working set.
    // Overlaps available/used memory; this is not a separate additive category.
    public ulong? CachedBytes { get; set; }
    public ulong? PageFileTotalBytes { get; set; }
    public ulong? PageFileUsedBytes { get; set; }
    // Not supplied by the documented query APIs used by MemoryAnalyzer.
    public ulong? CompressedBytes { get; set; }

    public double? PhysicalUsagePercentage => TotalBytes > 0
        ? UsedBytes / (double)TotalBytes * 100 : null;
    public double? CommitUsagePercentage => CommitUsageBytes.HasValue && CommitLimitBytes is > 0
        ? CommitUsageBytes.Value / (double)CommitLimitBytes.Value * 100 : null;
    public bool IsCommitNearLimit => CommitUsagePercentage is >= CommitWarningThresholdPercent;
    public string CommitPressureText => CommitUsagePercentage switch
    {
        null => "Commit percentage unavailable (usage or a nonzero limit was not reported).",
        >= 100 => "Commit usage is at or above the reported commit limit.",
        >= CommitWarningThresholdPercent => $"WinOptimizer warning: Commit usage is at or above the {CommitWarningThresholdPercent:0}% warning threshold.",
        _ => $"Commit usage is below WinOptimizer's {CommitWarningThresholdPercent:0}% warning threshold."
    };

    public double TotalGigabytes =>
        TotalBytes / 1024.0 / 1024.0 / 1024.0;

    public double AvailableGigabytes =>
        AvailableBytes / 1024.0 / 1024.0 / 1024.0;

    public double UsedGigabytes =>
        UsedBytes / 1024.0 / 1024.0 / 1024.0;
}