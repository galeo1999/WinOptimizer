namespace WinOptimizer.Models;

public class StorageCleanupResult
{
    // Recycle Bin counts are measured before/after; other applications may change them.
    public bool IsEstimate { get; set; }

    public long DeletedFiles { get; set; }

    public long DeletedBytes { get; set; }

    public long SkippedFiles { get; set; }

    public long FailedFiles { get; set; }
}