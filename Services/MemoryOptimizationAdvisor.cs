using WinOptimizer.Models;

namespace WinOptimizer.Services;

// Pure projection of existing snapshots: no process, registry or memory-management API calls.
public static class MemoryOptimizationAdvisor
{
    public static MemoryOptimizationAdvice Analyze(
        MemoryAnalysisResult memory, IReadOnlyList<StartupItem> startupItems)
    {
        var index = StartupProcessMatcher.CreateIndex(startupItems);
        var recommendations = memory.Processes
            .DistinctBy(process => process.ProcessId)
            .Select(process => new MemoryOptimizationRecommendation(process,
                StartupProcessMatcher.FindMatches(process.ExecutablePath, index).ToArray()))
            .ToArray();
        var matched = recommendations.Where(item => item.HasStartupAssociation).ToArray();
        var workingSets = matched.Select(item => item.Process.WorkingSetBytes).ToArray();
        var privateMemory = matched.Select(item => item.Process.PrivateMemoryBytes).ToArray();
        return new MemoryOptimizationAdvice(recommendations, memory.CapturedAt,
            SumAvailable(workingSets), SumAvailable(privateMemory), matched.Length,
            matched.Count(item => !item.Process.WorkingSetBytes.HasValue || !item.Process.PrivateMemoryBytes.HasValue));
    }

    private static ulong? SumAvailable(ulong?[] values) =>
        values.Length > 0 && values.All(value => !value.HasValue)
            ? null
            : values.Aggregate(0UL, (total, value) => checked(total + (value ?? 0)));

    public static IReadOnlyList<MemoryOptimizationRecommendation> FilterAndSort(
        MemoryOptimizationAdvice advice, MemoryAdvisorFilter filter, MemoryAdvisorSort sort)
    {
        IEnumerable<MemoryOptimizationRecommendation> rows = filter switch
        {
            MemoryAdvisorFilter.StartupApplications => advice.Recommendations.Where(row => row.HasStartupAssociation),
            MemoryAdvisorFilter.WithoutStartupAssociation => advice.Recommendations.Where(row => !row.HasStartupAssociation),
            _ => advice.Recommendations
        };
        IOrderedEnumerable<MemoryOptimizationRecommendation> ordered = sort switch
        {
            MemoryAdvisorSort.PrivateMemory => rows.OrderByDescending(row => row.Process.PrivateMemoryBytes),
            MemoryAdvisorSort.ProcessName => rows.OrderBy(row => row.Process.ProcessName, StringComparer.OrdinalIgnoreCase),
            _ => rows.OrderByDescending(row => row.Process.WorkingSetBytes)
        };
        return ordered.ThenBy(row => row.Process.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.Process.ProcessId).ToArray();
    }
}
