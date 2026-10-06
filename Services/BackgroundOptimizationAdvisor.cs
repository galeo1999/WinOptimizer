using WinOptimizer.Models;

namespace WinOptimizer.Services;

// Pure projection: no operating-system calls or changes. All categories are informational.
public static class BackgroundOptimizationAdvisor
{
    public static BackgroundOptimizationAdvice Analyze(BackgroundAnalysisResult snapshot,
        IReadOnlyList<StartupItem> startup)
    {
        var index = StartupProcessMatcher.CreateIndex(startup);
        var seen = new HashSet<int>();
        var rows = new List<BackgroundOptimizationRecommendation>();
        foreach (var activity in snapshot.Groups)
        {
            if (activity.Process is { } process && !seen.Add(process.ProcessId)) continue;
            var matches = StartupProcessMatcher.FindMatches(activity.Process?.ExecutablePath, index).ToArray();
            var type = BackgroundRecommendationType.NoActionableAssociation;
            var reasons = new List<string>();
            bool measured = activity.WorkingSetBytes is > 0 || activity.PrivateMemoryBytes is > 0;
            if (activity.Services.Count > 0 && (!activity.WorkingSetBytes.HasValue || !activity.PrivateMemoryBytes.HasValue))
            {
                type |= BackgroundRecommendationType.ServiceResourceDataUnavailable;
                reasons.Add("Service resource data is unavailable or partial; no per-service memory estimate is made.");
            }
            if (activity.Services.Count > 1)
            {
                type |= BackgroundRecommendationType.SharedServiceHost;
                reasons.Add("Shared service host: memory belongs to the host and cannot be attributed exactly to individual services.");
            }
            if (measured && activity.Services.Any(s => s.Status == "Running" && s.StartType == "Auto"))
            {
                type |= BackgroundRecommendationType.RunningAutomaticService;
                reasons.Add("Running automatic service with measurable host memory.");
            }
            if (activity.Services.Any(s => s.Status == "Running" && s.StartType == "Manual"))
            {
                type |= BackgroundRecommendationType.RunningManualService;
                reasons.Add("Manual service currently running; manual startup does not mean it is unnecessary.");
            }
            if (activity.Process != null && matches.Length == 1)
            {
                type |= BackgroundRecommendationType.StartupAssociatedProcess;
                reasons.Add("Running process has one exact executable-path association with an existing startup entry.");
            }
            if (activity.Process != null && activity.Services.Count == 0 && activity.HasMainWindow == false)
            {
                type |= BackgroundRecommendationType.BackgroundCandidate;
                reasons.Add("No main window was detected; background candidate only. Publisher origin is Unknown.");
            }
            if (type == BackgroundRecommendationType.NoActionableAssociation)
                reasons.Add("No actionable association: existing data does not support a more specific review category.");
            rows.Add(new(activity, matches, type, string.Join(" ", reasons),
                activity.Services.Count == 1 ? SupportedBackgroundActions.ForService(activity.Services[0]) : null));
        }
        var measuredRows = rows.Where(r => r.IncludedInMeasurement).ToArray();
        return new(rows, snapshot.CapturedAt,
            Sum(measuredRows.Select(r => r.WorkingSetBytes)), Sum(measuredRows.Select(r => r.PrivateMemoryBytes)),
            measuredRows.Length, measuredRows.Count(r => !r.WorkingSetBytes.HasValue));
    }

    private static ulong? Sum(IEnumerable<ulong?> values)
    {
        var array = values.ToArray();
        return array.Length > 0 && array.All(v => !v.HasValue) ? null
            : array.Aggregate(0UL, (sum, value) => checked(sum + (value ?? 0)));
    }

    public static IReadOnlyList<BackgroundOptimizationRecommendation> Filter(
        BackgroundOptimizationAdvice? advice, BackgroundAdvisorFilter filter)
    {
        var flag = filter switch
        {
            BackgroundAdvisorFilter.AutomaticServices => BackgroundRecommendationType.RunningAutomaticService,
            BackgroundAdvisorFilter.StartupAssociated => BackgroundRecommendationType.StartupAssociatedProcess,
            BackgroundAdvisorFilter.BackgroundCandidates => BackgroundRecommendationType.BackgroundCandidate,
            BackgroundAdvisorFilter.SharedHosts => BackgroundRecommendationType.SharedServiceHost,
            _ => BackgroundRecommendationType.NoActionableAssociation
        };
        return (advice?.Recommendations ?? []).Where(r => flag == 0 || r.RecommendationType.HasFlag(flag))
            .OrderByDescending(r => r.WorkingSetBytes).ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
