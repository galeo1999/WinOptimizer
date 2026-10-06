using WinOptimizer.Models;

namespace WinOptimizer.Services;

internal interface IServiceConfigurationAccess
{
    ServiceConfiguration Read(SupportedBackgroundOptimization action);
    void Write(SupportedBackgroundOptimization action, ServiceConfiguration target);
}
internal interface IBackgroundServiceHistoryStore
{
    IDisposable AcquireWriteLock();
    IReadOnlyList<BackgroundServiceHistoryEntry> Load();
    BackgroundServiceHistoryEntry Get(Guid id);
    void Save(BackgroundServiceHistoryEntry entry);
}

public sealed class BackgroundServiceManager
{
    private readonly IServiceConfigurationAccess _configuration;
    private readonly IBackgroundServiceHistoryStore _history;
    private readonly Func<bool> _isAdmin;
    public BackgroundServiceManager() : this(new WindowsServiceConfigurationAccess(), new BackgroundServiceHistoryStore(),
        AdminService.IsRunningAsAdministrator) { }
    internal BackgroundServiceManager(IServiceConfigurationAccess configuration, IBackgroundServiceHistoryStore history,
        Func<bool> isAdmin) { _configuration = configuration; _history = history; _isAdmin = isAdmin; }

    public IReadOnlyList<BackgroundServiceHistoryEntry> GetHistory() => _history.Load();

    public BackgroundServiceChangePlan PreviewApply(string actionId)
    {
        var action = SupportedBackgroundActions.Resolve(actionId);
        var current = _configuration.Read(action);
        if (current.StartupType != ServiceStartupType.Automatic)
            throw new InvalidOperationException("Only an Automatic → Manual change is supported. Refresh Background; the configuration may have changed.");
        return new(action, current, current with { StartupType = action.TargetStartupType, DelayedAutoStart = false }, null);
    }

    public BackgroundServiceChangePlan PreviewRestore(Guid historyId)
    {
        var entry = _history.Get(historyId);
        var action = ValidateHistory(entry);
        if (entry.Status == "Restored") throw new InvalidOperationException("This change is already restored.");
        if (_history.Load().Any(other => other.ServiceName == entry.ServiceName && other.Timestamp > entry.Timestamp && other.Status != "Restored"))
            throw new InvalidOperationException("Restore the newer background change for this service first.");
        var current = _configuration.Read(action);
        // An interrupted Automatic(delayed) → Manual write may have cleared only the delayed flag.
        bool interruptedTransition = (entry.Status is "Pending" or "Failed" || entry.RestoreStatus == "Pending") &&
            current.SameSettings(entry.PreviousConfiguration with { DelayedAutoStart = false });
        if (!interruptedTransition && !current.SameSettings(entry.NewConfiguration) && !current.SameSettings(entry.PreviousConfiguration))
            throw new InvalidOperationException("The service configuration changed outside this restore point. No settings will be overwritten.");
        return new(action, current, entry.PreviousConfiguration, historyId);
    }

    private static SupportedBackgroundOptimization ValidateHistory(BackgroundServiceHistoryEntry entry)
    {
        var action = SupportedBackgroundActions.Resolve(entry.ActionId);
        if (entry.Id == Guid.Empty || entry.ServiceName != action.ServiceName || entry.PreviousConfiguration == null ||
            entry.NewConfiguration == null || entry.PreviousConfiguration.StartupType != ServiceStartupType.Automatic ||
            entry.NewConfiguration.StartupType != action.TargetStartupType || entry.NewConfiguration.DelayedAutoStart ||
            entry.PreviousConfiguration.BinaryPath != entry.NewConfiguration.BinaryPath ||
            entry.PreviousConfiguration.ServiceType != entry.NewConfiguration.ServiceType)
            throw new InvalidOperationException("The background restore record is invalid or unsupported.");
        return action;
    }

    public BackgroundServiceChangeResult ExecuteConfirmed(BackgroundServiceChangePlan plan)
    {
        if (!_isAdmin()) throw new UnauthorizedAccessException("Administrator approval is required.");
        using var writeLock = _history.AcquireWriteLock();
        // Read again under the cross-process lock. A changed configuration requires a new confirmation.
        var fresh = plan.RestoreId is { } id ? PreviewRestore(id) : PreviewApply(plan.Action.Id);
        if (fresh.Action != plan.Action || !fresh.Current.SameSettings(plan.Current) || !fresh.Target.SameSettings(plan.Target))
            throw new InvalidOperationException("Configuration changed since confirmation. Review the change again.");
        bool restore = fresh.RestoreId.HasValue;
        var entry = restore ? _history.Get(fresh.RestoreId!.Value) : new BackgroundServiceHistoryEntry
        {
            ActionId = fresh.Action.Id, ServiceName = fresh.Action.ServiceName,
            PreviousConfiguration = fresh.Current, NewConfiguration = fresh.Target
        };
        if (restore)
        {
            entry.RestoreStatus = "Pending";
            entry.RestoreAttemptFrom = fresh.Current;
        }
        // Durable backup BEFORE any native configuration change. Failure here aborts the operation.
        _history.Save(entry);
        try
        {
            if (!fresh.Current.SameSettings(fresh.Target)) _configuration.Write(fresh.Action, fresh.Target);
            if (!_configuration.Read(fresh.Action).SameSettings(fresh.Target))
                throw new InvalidOperationException("Windows did not report the requested configuration after the change.");
            entry.Status = restore ? "Restored" : "Applied";
            entry.Error = null;
            entry.RollbackStatus = null;
            if (restore) { entry.RestoreStatus = "Restored"; entry.RestoredAt = DateTimeOffset.Now; }
            _history.Save(entry);
            return new(true, entry.Id, restore ? "Previous startup configuration restored and verified. Running state was not changed by WinOptimizer."
                : "Manual startup applied and verified. The running service was not stopped. A restore point is available in Restore → Background Changes.");
        }
        catch (Exception ex)
        {
            entry.Status = "Failed";
            if (restore) { entry.RestoreStatus = "Failed"; entry.RestoredAt = null; }
            entry.Error = ex.Message;
            try
            {
                var observed = _configuration.Read(fresh.Action);
                bool knownTransition = observed.SameSettings(fresh.Target) || observed.SameSettings(fresh.Current) ||
                    observed.SameSettings(fresh.Current with { DelayedAutoStart = false }) ||
                    observed.SameSettings(fresh.Target with { DelayedAutoStart = false });
                if (!knownTransition)
                    entry.RollbackStatus = "Rollback skipped: configuration changed outside the requested transition.";
                else
                {
                    if (!observed.SameSettings(fresh.Current)) _configuration.Write(fresh.Action, fresh.Current);
                    entry.RollbackStatus = _configuration.Read(fresh.Action).SameSettings(fresh.Current)
                        ? "Previous configuration verified" : "Rollback verification failed";
                }
            }
            catch (Exception rollback) { entry.RollbackStatus = "Rollback failed: " + rollback.Message; }
            try { _history.Save(entry); }
            catch (Exception historyError) { entry.Error += " History update failed: " + historyError.Message + ". The write-ahead backup remains available."; }
            return new(false, entry.Id, $"Change failed: {entry.Error}\n{entry.RollbackStatus}");
        }
    }
}
