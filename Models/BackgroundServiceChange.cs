namespace WinOptimizer.Models;

public enum ServiceStartupType : uint { Automatic = 2, Manual = 3, Disabled = 4 }

public sealed record ServiceConfiguration(ServiceStartupType StartupType, bool DelayedAutoStart,
    uint RunningState, string BinaryPath, uint ServiceType)
{
    public string StartupDescription => StartupType == ServiceStartupType.Automatic && DelayedAutoStart
        ? "Automatic (Delayed Start)" : StartupType.ToString();
    public bool SameSettings(ServiceConfiguration other) => StartupType == other.StartupType &&
        DelayedAutoStart == other.DelayedAutoStart && BinaryPath == other.BinaryPath && ServiceType == other.ServiceType;
}

public sealed record SupportedBackgroundOptimization(string Id, string ServiceName, string DisplayName,
    string Description, string Consequence, ServiceStartupType TargetStartupType);

public sealed class BackgroundServiceHistoryEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ActionId { get; set; } = "";
    public string ServiceName { get; set; } = "";
    public string Action { get; set; } = "Change service startup configuration";
    public ServiceConfiguration PreviousConfiguration { get; set; } = null!;
    public ServiceConfiguration NewConfiguration { get; set; } = null!;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
    public string Status { get; set; } = "Pending";
    public string RestoreStatus { get; set; } = "Not requested";
    public DateTimeOffset? RestoredAt { get; set; }
    public string? Error { get; set; }
    public string? RollbackStatus { get; set; }
    public ServiceConfiguration? RestoreAttemptFrom { get; set; }
}

// Constructed only by a live preview. The manager revalidates every field before a write.
public sealed class BackgroundServiceChangePlan
{
    internal BackgroundServiceChangePlan(SupportedBackgroundOptimization action, ServiceConfiguration current,
        ServiceConfiguration target, Guid? restoreId)
    { Action = action; Current = current; Target = target; RestoreId = restoreId; }
    public SupportedBackgroundOptimization Action { get; }
    public ServiceConfiguration Current { get; }
    public ServiceConfiguration Target { get; }
    public Guid? RestoreId { get; }
}

public sealed record BackgroundServiceChangeResult(bool Success, Guid HistoryId, string Message);
