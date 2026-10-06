using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using WinOptimizer.Models;

namespace WinOptimizer.Services;

// No stop/start/delete/control or service-security APIs. Handles have query rights and, on writes, CHANGE_CONFIG only.
internal sealed class WindowsServiceConfigurationAccess : IServiceConfigurationAccess
{
    private const uint QueryConfig = 1, ChangeConfig = 2, QueryStatus = 4, NoChange = uint.MaxValue;
    public ServiceConfiguration Read(SupportedBackgroundOptimization action)
    {
        using var service = Open(action, QueryConfig | QueryStatus);
        return Read(service, action);
    }

    public void Write(SupportedBackgroundOptimization action, ServiceConfiguration target)
    {
        action = SupportedBackgroundActions.Resolve(action.Id);
        if (target.StartupType is not (ServiceStartupType.Automatic or ServiceStartupType.Manual) ||
            (target.StartupType == ServiceStartupType.Manual && target.DelayedAutoStart))
            throw new InvalidOperationException("Unsupported service configuration.");
        using var service = Open(action, QueryConfig | QueryStatus | ChangeConfig);
        var current = Read(service, action);
        if (current.BinaryPath != target.BinaryPath || current.ServiceType != target.ServiceType)
            throw new InvalidOperationException("Service identity changed; no configuration was written.");
        // Clear the delayed flag while still Automatic; restore it only after restoring Automatic startup.
        if (target.StartupType == ServiceStartupType.Manual && current.DelayedAutoStart)
        {
            int delayed = 0;
            Check(ChangeServiceConfig2(service, 3, ref delayed));
        }
        Check(ChangeServiceConfig(service, NoChange, (uint)target.StartupType, NoChange,
            null, null, IntPtr.Zero, null, null, null, null));
        if (target.StartupType == ServiceStartupType.Automatic)
        {
            int delayed = target.DelayedAutoStart ? 1 : 0;
            Check(ChangeServiceConfig2(service, 3, ref delayed));
        }
    }

    private static ServiceHandle Open(SupportedBackgroundOptimization action, uint access)
    {
        action = SupportedBackgroundActions.Resolve(action.Id); // Never trust an external service name.
        using var scm = OpenSCManager(null, null, 1); // SC_MANAGER_CONNECT
        if (scm.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
        var service = OpenService(scm, action.ServiceName, access);
        if (service.IsInvalid) { int error = Marshal.GetLastWin32Error(); service.Dispose(); throw new Win32Exception(error); }
        return service;
    }

    private static ServiceConfiguration Read(ServiceHandle service, SupportedBackgroundOptimization action)
    {
        QueryServiceConfig(service, IntPtr.Zero, 0, out uint needed);
        if (needed == 0 || needed > 1024 * 1024) throw new Win32Exception(Marshal.GetLastWin32Error());
        IntPtr buffer = Marshal.AllocHGlobal(checked((int)needed));
        try
        {
            Check(QueryServiceConfig(service, buffer, needed, out _));
            var config = Marshal.PtrToStructure<NativeConfiguration>(buffer);
            Check(QueryServiceConfig2(service, 3, out int delayed, sizeof(int), out _));
            Check(QueryServiceStatus(service, out NativeStatus status));
            var result = new ServiceConfiguration((ServiceStartupType)config.StartType, delayed != 0,
                status.CurrentState, Marshal.PtrToStringUni(config.BinaryPath) ?? "", config.ServiceType);
            ValidateIdentity(action, result);
            return result;
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    internal static void ValidateIdentity(SupportedBackgroundOptimization action, ServiceConfiguration configuration)
    {
        if (action.Id != SupportedBackgroundActions.SearchManualId || action.ServiceName != "WSearch" || configuration.ServiceType != 0x10)
            throw new InvalidOperationException("Unsupported service identity or service type.");
        string command = Environment.ExpandEnvironmentVariables(configuration.BinaryPath).Trim();
        string executable;
        if (command.StartsWith('"'))
        {
            int end = command.IndexOf('"', 1);
            if (end < 0 || (end + 1 < command.Length && !char.IsWhiteSpace(command[end + 1])))
                throw new InvalidOperationException("Ambiguous service executable path.");
            executable = command[1..end];
        }
        else executable = command.Split([' ', '\t'], 2)[0];
        string expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "SearchIndexer.exe");
        if (!Path.IsPathFullyQualified(executable) || !string.Equals(Path.GetFullPath(executable), expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Windows Search does not have the expected system executable; the action is unavailable.");
    }

    private static void Check(bool result) { if (!result) throw new Win32Exception(Marshal.GetLastWin32Error()); }
    [StructLayout(LayoutKind.Sequential)] private struct NativeConfiguration
    {
        public uint ServiceType, StartType, ErrorControl;
        public IntPtr BinaryPath, LoadOrderGroup;
        public uint TagId;
        public IntPtr Dependencies, ServiceStartName, DisplayName;
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativeStatus
    { public uint ServiceType, CurrentState, ControlsAccepted, Win32ExitCode, ServiceSpecificExitCode, CheckPoint, WaitHint; }
    private sealed class ServiceHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public ServiceHandle() : base(true) { }
        protected override bool ReleaseHandle() => CloseServiceHandle(handle);
    }
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ServiceHandle OpenSCManager(string? machine, string? database, uint access);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ServiceHandle OpenService(ServiceHandle manager, string name, uint access);
    [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseServiceHandle(IntPtr handle);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryServiceConfig(ServiceHandle service, IntPtr config, uint size, out uint needed);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryServiceConfig2(ServiceHandle service, uint level, out int info, uint size, out uint needed);
    [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryServiceStatus(ServiceHandle service, out NativeStatus status);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ChangeServiceConfig(ServiceHandle service, uint type, uint start, uint error,
        string? binary, string? group, IntPtr tag, string? dependencies, string? account, string? password, string? display);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ChangeServiceConfig2(ServiceHandle service, uint level, ref int info);
}
