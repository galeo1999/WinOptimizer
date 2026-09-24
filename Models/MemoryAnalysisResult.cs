namespace WinOptimizer.Models;

public sealed record MemoryAnalysisResult(
    MemorySnapshot Snapshot,
    IReadOnlyList<ProcessMemoryInfo> Processes,
    DateTimeOffset CapturedAt);
