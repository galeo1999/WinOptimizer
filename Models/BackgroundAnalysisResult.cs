namespace WinOptimizer.Models;

public sealed record BackgroundAnalysisResult(
    IReadOnlyList<BackgroundProcessInfo> Groups,
    IReadOnlyList<WindowsServiceInfo> Services,
    DateTimeOffset CapturedAt,
    string? Warning)
{
    public int RunningServices => Services.Count(s => s.Status == "Running");
    public int ServiceHostProcesses => Groups.Count(g => g.Process != null && g.Services.Count > 0);
    public int UnavailableHosts => Groups.Count(g => g.Process != null && !g.WorkingSetBytes.HasValue);
    public ulong? MeasuredWorkingSetBytes => SumMeasured(Groups.Select(g => g.WorkingSetBytes));
    public ulong? MeasuredPrivateBytes => SumMeasured(Groups.Select(g => g.PrivateMemoryBytes));
    private static ulong? SumMeasured(IEnumerable<ulong?> values)
    {
        ulong[] measured = values.Where(v => v.HasValue).Select(v => v!.Value).ToArray();
        return measured.Length == 0 ? null : measured.Aggregate(0UL, (a, b) => a + b);
    }
}
