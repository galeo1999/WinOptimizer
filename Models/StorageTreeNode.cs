namespace WinOptimizer.Models;

public class StorageTreeNode
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public long SizeBytes { get; set; }
    public long FileCount { get; set; }
    // Includes skipped descendants; an unreadable directory counts as one item.
    public long SkippedItems { get; set; }
    public List<StorageTreeNode> Children { get; set; } = [];
}
