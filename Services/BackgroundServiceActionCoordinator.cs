using WinOptimizer.Models;

namespace WinOptimizer.Services;

// Separates the mandatory confirmation/elevation gate from dialog rendering, so cancellation is testable without UAC or service writes.
internal sealed class BackgroundServiceActionCoordinator(
    BackgroundServiceManager manager,
    Func<BackgroundServiceChangePlan, bool> confirm,
    Func<bool> isAdmin,
    Func<string?, Guid?, Task<int?>> elevate,
    Action<string, bool> show)
{
    internal async Task<int> RunAsync(string? actionId, Guid? restoreId, bool elevatedCommand = false)
    {
        try
        {
            if ((actionId == null) == (restoreId == null)) throw new ArgumentException("Specify one background operation.");
            if (elevatedCommand && !isAdmin()) throw new UnauthorizedAccessException("This operation requires administrator approval.");
            var plan = await Task.Run(() => restoreId.HasValue ? manager.PreviewRestore(restoreId.Value) : manager.PreviewApply(actionId!));
            if (!confirm(plan)) return 2;
            if (!isAdmin())
            {
                int? exit = await elevate(actionId, restoreId);
                if (exit == null || exit == 2) return 2;
                if (exit != 0) show("The elevated operation failed. Review Background Changes in Restore for any recorded result.", true);
                return exit == 0 ? 0 : 1;
            }
            var result = await Task.Run(() => manager.ExecuteConfirmed(plan));
            show(result.Message, !result.Success);
            return result.Success ? 0 : 1;
        }
        catch (Exception ex) { show(ex.Message, true); return 1; }
    }
}
