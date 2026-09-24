namespace WinOptimizer.Models;

public class StorageCleanupResult
{
    public long DeletedFiles { get; set; }

    public long DeletedBytes { get; set; }

    public long SkippedFiles { get; set; }

    public long FailedFiles { get; set; }
}