namespace WinOptimizer.Models;

public class TweakHistoryEntry
{
    public string Id { get; set; } =
        Guid.NewGuid().ToString();

    public string TweakId { get; set; } = "";

    public string TweakName { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; } =
        DateTimeOffset.Now;

    public string Status { get; set; } =
        "Pending";

    public DateTimeOffset? RestoredAt { get; set; }

    public List<RegistryBackup> RegistryBackups { get; set; } = [];
}