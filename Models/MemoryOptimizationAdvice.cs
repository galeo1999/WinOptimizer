namespace WinOptimizer.Models;

public enum MemoryAdvisorFilter { All, StartupApplications, WithoutStartupAssociation }
public enum MemoryAdvisorSort { WorkingSet, PrivateMemory, ProcessName }

public sealed record MemoryOptimizationRecommendation(
    ProcessMemoryInfo Process,
    IReadOnlyList<StartupItem> StartupMatches)
{
    public bool HasStartupAssociation => StartupMatches.Count == 1;
    public StartupItem? StartupEntry => HasStartupAssociation ? StartupMatches[0] : null;
    public bool CanManageStartup => StartupEntry is
        { State: not StartupState.Unknown, SourceType: StartupSourceType.Registry or StartupSourceType.StartupFolder };
    public string Category => StartupMatches.Count > 1
        ? "Multiple startup entries match this path; association is ambiguous"
        : StartupEntry?.State switch
        {
            StartupState.Enabled => Process.WorkingSetBytes is > 0
                ? "Starts with Windows and is currently using memory"
                : "Startup enabled; process found in the current snapshot",
            StartupState.Disabled => "Startup already disabled",
            StartupState.Unknown => "Startup state unknown",
            _ => "No startup association found"
        };
    public string StartupStateText => StartupEntry?.State.ToString() ?? "No unique association";
    public string StartupSourceText => StartupEntry is { } entry
        ? $"{entry.Name} · {entry.SourceType} · {entry.Scope} · {entry.Source}"
        : "No unique startup source";
    public string AdminRequirementText => StartupEntry is { } entry
        ? entry.RequiresAdmin ? "Administrator privileges required to change startup" : "No administrator privileges required to change startup"
        : "Startup privilege requirement unavailable";
}

public sealed record MemoryOptimizationAdvice(
    IReadOnlyList<MemoryOptimizationRecommendation> Recommendations,
    DateTimeOffset CapturedAt,
    ulong? CurrentWorkingSetBytes,
    ulong? CurrentPrivateMemoryBytes,
    int MatchedProcessCount,
    int UnavailableProcessCount);
