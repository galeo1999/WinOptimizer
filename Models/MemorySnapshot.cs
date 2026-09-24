namespace WinOptimizer.Models;

public class MemorySnapshot
{
    public ulong TotalBytes { get; set; }

    public ulong AvailableBytes { get; set; }

    public ulong UsedBytes { get; set; }

    public uint UsagePercentage { get; set; }

    public double TotalGigabytes =>
        TotalBytes / 1024.0 / 1024.0 / 1024.0;

    public double AvailableGigabytes =>
        AvailableBytes / 1024.0 / 1024.0 / 1024.0;

    public double UsedGigabytes =>
        UsedBytes / 1024.0 / 1024.0 / 1024.0;
}