namespace WinOptimizer.Models;

public class StartupHistoryEntry
{
    public string Id { get; set; } =
        Guid.NewGuid().ToString();

    public string StartupName { get; set; } = "";

    public string Action { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; } =
        DateTimeOffset.Now;

    public string Status { get; set; } =
        "Pending";

    public DateTimeOffset? RestoredAt { get; set; }

    public StartupBackup Backup { get; set; } =
        new StartupBackup();
}