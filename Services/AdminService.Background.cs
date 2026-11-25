using System.ComponentModel;
using System.Diagnostics;

namespace WinOptimizer.Services;

public static partial class AdminService
{
    // The original standard-user window remains open; only the single operation runs elevated.
    public static async Task<int?> RunElevatedBackgroundAsync(string? actionId, Guid? restoreId)
    {
        if ((actionId == null) == (restoreId == null)) throw new ArgumentException("Specify exactly one background operation.");
        if (actionId != null) SupportedBackgroundActions.Resolve(actionId);
        if (restoreId == Guid.Empty) throw new ArgumentException("Invalid restore identifier.");
        var start = new ProcessStartInfo(Environment.ProcessPath ?? throw new InvalidOperationException("Executable unavailable."))
        { UseShellExecute = true, Verb = "runas", WorkingDirectory = AppContext.BaseDirectory };
        start.ArgumentList.Add(actionId != null ? "--background-apply" : "--background-restore");
        start.ArgumentList.Add(actionId ?? restoreId!.Value.ToString("D"));
        try
        {
            using var child = Process.Start(start) ?? throw new InvalidOperationException("The elevated operation did not start.");
            await child.WaitForExitAsync();
            return child.ExitCode;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { return null; }
    }
}
