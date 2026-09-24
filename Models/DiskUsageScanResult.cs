namespace WinOptimizer.Models;

public sealed record DiskUsageScanResult(
    StorageTreeNode Root,
    IReadOnlyList<LargeFileInfo> LargestFiles);

public sealed record DiskUsageProgress(
    string CurrentPath,
    long SizeBytes,
    long FileCount,
    long SkippedItems);
