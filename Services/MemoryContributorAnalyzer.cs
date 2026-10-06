using System.IO;
using WinOptimizer.Models;

namespace WinOptimizer.Services;

// Groups already-collected counters; never queries, modifies or terminates processes.
public static class MemoryContributorAnalyzer
{
    public static IReadOnlyList<MemoryApplicationGroup> Analyze(IEnumerable<ProcessMemoryInfo> processes)
    {
        var groups = new Dictionary<string, List<ProcessMemoryInfo>>(StringComparer.Ordinal);
        var paths = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (ProcessMemoryInfo process in processes.DistinctBy(process => process.ProcessId))
        {
            string? path = NormalizePath(process.ExecutablePath);
            // Be conservative even on case-sensitive Windows directories: only exact normalized
            // paths coalesce. Missing paths must not merge by process name, even identical names.
            string key = path == null ? $"pid:{process.ProcessId}" : $"path:{path}";
            if (!groups.TryGetValue(key, out var members))
            {
                groups[key] = members = [];
                paths[key] = path;
            }
            members.Add(process);
        }
        return groups.Select(pair =>
        {
            string? path = paths[pair.Key];
            ProcessMemoryInfo[] members = pair.Value.OrderByDescending(process => process.WorkingSetBytes)
                .ThenBy(process => process.ProcessId).ToArray();
            string name = path == null
                ? $"{members[0].ProcessName} (PID {members[0].ProcessId})"
                : Path.GetFileNameWithoutExtension(path);
            return new MemoryApplicationGroup(name, path, members,
                SumKnown(members.Select(process => process.WorkingSetBytes)),
                SumKnown(members.Select(process => process.PrivateMemoryBytes)));
        }).ToArray();
    }

    private static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            return null;
        try { return Path.GetFullPath(path); }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
        {
            return null;
        }
    }

    private static ulong? SumKnown(IEnumerable<ulong?> values)
    {
        ulong? total = null;
        foreach (ulong? value in values)
            if (value.HasValue)
                total = checked((total ?? 0) + value.Value);
        return total;
    }

    public static IReadOnlyList<MemoryApplicationGroup> Sort(
        IEnumerable<MemoryApplicationGroup> groups, MemoryContributorSort sort)
    {
        IOrderedEnumerable<MemoryApplicationGroup> ordered = sort switch
        {
            MemoryContributorSort.PrivateMemory => groups.OrderByDescending(group => group.CombinedPrivateMemoryBytes),
            MemoryContributorSort.ProcessCount => groups.OrderByDescending(group => group.ProcessCount),
            MemoryContributorSort.ApplicationName => groups.OrderBy(group => group.ApplicationName, StringComparer.OrdinalIgnoreCase),
            _ => groups.OrderByDescending(group => group.CombinedWorkingSetBytes)
        };
        return ordered.ThenBy(group => group.ApplicationName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(group => group.ExecutablePath, StringComparer.Ordinal).ToArray();
    }

    public static MemoryApplicationGroup? LargestWorkingSet(IEnumerable<MemoryApplicationGroup> groups) =>
        Sort(groups.Where(group => group.CombinedWorkingSetBytes.HasValue), MemoryContributorSort.WorkingSet)
            .FirstOrDefault();
}
