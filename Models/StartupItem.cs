namespace WinOptimizer.Models;

public class StartupItem
{
    public string Name { get; set; } = "";

    public string Command { get; set; } = "";

    public string Source { get; set; } = "";

    public string Scope { get; set; } = "";

    public bool RequiresAdmin { get; set; }

    public StartupState State { get; set; } =
        StartupState.Unknown;
    public string StateSource { get; set; } = "";
    public StartupSourceType SourceType { get; set; }
    public string StartupApprovedHive { get; set; } = "";

    public string StartupApprovedPath { get; set; } = "";

    public string StartupApprovedValueName { get; set; } = "";
}