namespace WinOptimizer.Models;

[Flags]
public enum BackgroundRecommendationType
{
    NoActionableAssociation = 0,
    RunningAutomaticService = 1,
    StartupAssociatedProcess = 2,
    BackgroundCandidate = 4,
    SharedServiceHost = 8,
    ServiceResourceDataUnavailable = 16,
    RunningManualService = 32
}

public enum BackgroundAdvisorFilter { All, AutomaticServices, StartupAssociated, BackgroundCandidates, SharedHosts }

// Every row is informational, including core Windows components. No disabling advice.
public sealed record BackgroundOptimizationRecommendation(
    BackgroundProcessInfo Activity,
    IReadOnlyList<StartupItem> StartupMatches,
    BackgroundRecommendationType RecommendationType,
    string Reason)
{
    public string Name => Activity.Name;
    public string? ServiceName => Activity.Services.Count == 1 ? Activity.Services[0].ServiceName : null;
    public int? ProcessId => Activity.Process?.ProcessId;
    public ulong? WorkingSetBytes => Activity.WorkingSetBytes;
    public ulong? PrivateMemoryBytes => Activity.PrivateMemoryBytes;
    public string StartupType => Activity.StartupType;
    public string CurrentStatus => Activity.Services.Count > 0 ? Activity.ServiceState
        : Activity.Process != null ? "Running at capture time" : "Unknown";
    public string Origin => "Unknown";
    public bool IsSharedHost => Activity.Services.Count > 1;
    public bool CanOpenLocation => Activity.Process?.CanOpenLocation == true;
    // Navigation only; the existing Startup UI decides which actions are supported.
    public bool CanManageStartup => StartupMatches.Count == 1;
    public StartupItem? StartupEntry => CanManageStartup ? StartupMatches[0] : null;
    // Only the known startup entry's requirement; service-change permissions are not probed.
    public bool? RequiresAdmin => StartupEntry?.RequiresAdmin;
    public string AdminRequirement => RequiresAdmin switch
    {
        true => "Associated startup changes require administrator privileges.",
        false => "Associated startup changes do not require administrator privileges.",
        _ => "Change permissions unknown; this advisor performs no changes."
    };
    public string StartupAssociation => StartupEntry is { } entry ? $"{entry.Name} · {entry.State} · {entry.SourceType}"
        : StartupMatches.Count > 1 ? "Ambiguous executable-path match; navigation unavailable" : "No unique startup association";
    public bool IncludedInMeasurement => Activity.Process != null && RecommendationType != BackgroundRecommendationType.NoActionableAssociation;
}

public sealed record BackgroundOptimizationAdvice(
    IReadOnlyList<BackgroundOptimizationRecommendation> Recommendations,
    DateTimeOffset CapturedAt,
    ulong? CurrentWorkingSetBytes,
    ulong? CurrentPrivateMemoryBytes,
    int ReviewableProcessCount,
    int UnavailableWorkingSetCount);
