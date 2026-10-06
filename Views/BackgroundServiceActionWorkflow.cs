using System.Windows;
using WinOptimizer.Services;

namespace WinOptimizer.Views;

internal static class BackgroundServiceActionWorkflow
{
    // Both normal UI and elevated command use the same mandatory confirmation gate.
    internal static Task<int> RunAsync(Window? owner, string? actionId, Guid? restoreId, bool elevatedCommand = false)
    {
        var coordinator = new BackgroundServiceActionCoordinator(new BackgroundServiceManager(), plan =>
        {
            var dialog = new BackgroundServiceConfirmation(plan);
            if (owner != null) { dialog.Owner = owner; dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner; }
            return dialog.ShowDialog() == true;
        }, AdminService.IsRunningAsAdministrator, AdminService.RunElevatedBackgroundAsync,
            (message, error) => Show(owner, message, error));
        return coordinator.RunAsync(actionId, restoreId, elevatedCommand);
    }

    private static void Show(Window? owner, string message, bool error)
    {
        if (owner == null) MessageBox.Show(message, "Background configuration", MessageBoxButton.OK, error ? MessageBoxImage.Error : MessageBoxImage.Information);
        else MessageBox.Show(owner, message, "Background configuration", MessageBoxButton.OK, error ? MessageBoxImage.Error : MessageBoxImage.Information);
    }
}
