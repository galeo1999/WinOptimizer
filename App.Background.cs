using System.Windows;
using WinOptimizer.Views;

namespace WinOptimizer;

public partial class App
{
    private bool TryHandleBackgroundCommand(string[] args)
    {
        if (!args.Any(a => a.StartsWith("--background-", StringComparison.OrdinalIgnoreCase))) return false;
        // Only the fixed action ID or a history GUID crosses the elevation boundary. No service names/configuration payloads.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _ = RunBackgroundCommandAsync(args);
        return true;
    }
    private async Task RunBackgroundCommandAsync(string[] args)
    {
        int exitCode = 1;
        try
        {
            if (args.Length != 2) throw new ArgumentException("Invalid background command arguments.");
            if (args[0] == "--background-apply")
                exitCode = await BackgroundServiceActionWorkflow.RunAsync(null, args[1], null, true);
            else if (args[0] == "--background-restore" && Guid.TryParseExact(args[1], "D", out Guid id) && id != Guid.Empty)
                exitCode = await BackgroundServiceActionWorkflow.RunAsync(null, null, id, true);
            else throw new ArgumentException("Unsupported background command.");
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Background configuration", MessageBoxButton.OK, MessageBoxImage.Error); }
        Shutdown(exitCode);
    }
}
