namespace WinOptimizer.Models;

public class StorageLocationInfo
{
    public string Name { get; set; } = "";

    public string Path { get; set; } = "";

    public string Description { get; set; } = "";

    public long SizeBytes { get; set; }

    public long FileCount { get; set; }

    public int SkippedItems { get; set; }

    public bool IsPartial =>
        SkippedItems > 0;

    public double SizeMegabytes =>
        SizeBytes / 1024d / 1024d;

    public double SizeGigabytes =>
        SizeBytes / 1024d / 1024d / 1024d;
        public bool IsCleanupCandidate { get; set; }

public bool RequiresAdmin { get; set; }

public bool RequiresConfirmation { get; set; }
public StorageLocationType Type { get; set; }
}