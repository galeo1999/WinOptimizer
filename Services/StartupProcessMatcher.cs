using System.IO;
using WinOptimizer.Models;

namespace WinOptimizer.Services;

// Shared by memory analysis and the advisor. No executable-name-only matching.
public static class StartupProcessMatcher
{
    public static Dictionary<string, List<StartupItem>> CreateIndex(IEnumerable<StartupItem> items)
    {
        var matches = new Dictionary<string, List<StartupItem>>(StringComparer.OrdinalIgnoreCase);
        foreach (StartupItem item in items)
        {
            // Shortcuts and packaged task relative paths need separate resolution; do not guess.
            if (item.SourceType == StartupSourceType.PackagedStartupTask)
                continue;
            string command = Environment.ExpandEnvironmentVariables(item.Command).Trim();
            string executable;
            if (command.StartsWith('"'))
            {
                int closingQuote = command.IndexOf('"', 1);
                if (closingQuote < 0 ||
                    (closingQuote + 1 < command.Length && !char.IsWhiteSpace(command[closingQuote + 1])))
                    continue;
                executable = command[1..closingQuote];
            }
            else
            {
                // An unquoted command with spaces is ambiguous; match only its first token.
                int end = command.IndexOfAny([' ', '\t', '\r', '\n']);
                executable = end < 0 ? command : command[..end];
            }
            if (!Path.IsPathFullyQualified(executable) ||
                !executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                continue;
            try
            {
                executable = Path.GetFullPath(executable);
                if (!matches.TryGetValue(executable, out List<StartupItem>? entries))
                    matches[executable] = entries = [];
                entries.Add(item);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException) { }
        }
        return matches;
    }


    public static IReadOnlyList<StartupItem> FindMatches(
        string? executablePath, IReadOnlyDictionary<string, List<StartupItem>> index)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !Path.IsPathFullyQualified(executablePath))
            return [];
        try
        {
            return index.TryGetValue(Path.GetFullPath(executablePath), out var entries) ? entries : [];
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException)
        {
            return [];
        }
    }

    public static string Describe(IReadOnlyList<StartupItem> matches) => matches.Count == 0
        ? "No confirmed match"
        : string.Join("; ", matches.Select(item => $"{item.Name} ({item.State})"));
}
