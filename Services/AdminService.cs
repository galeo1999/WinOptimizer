using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

namespace WinOptimizer.Services;

public static class AdminService
{
    public static bool IsRunningAsAdministrator()
    {
        using WindowsIdentity identity =
            WindowsIdentity.GetCurrent();

        WindowsPrincipal principal =
            new WindowsPrincipal(identity);

        return principal.IsInRole(
            WindowsBuiltInRole.Administrator
        );
    }

   public static bool TryStartElevatedInstance(
    string? arguments = null)
{
    string? executablePath =
        Environment.ProcessPath;

    if (string.IsNullOrWhiteSpace(executablePath))
    {
        throw new InvalidOperationException(
            "Could not determine the WinOptimizer executable path."
        );
    }

    try
    {
        ProcessStartInfo startInfo =
            new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = arguments ?? "",
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory =
                    AppContext.BaseDirectory
            };

        Process.Start(startInfo);

        return true;
    }
    catch (Win32Exception ex)
        when (ex.NativeErrorCode == 1223)
    {
        return false;
    }
}
}