using WinOptimizer.Models;

namespace WinOptimizer.Services;

public static class SupportedBackgroundActions
{
    public const string SearchManualId = "windows-search-manual";
    private static readonly SupportedBackgroundOptimization SearchManual = new(SearchManualId, "WSearch", "Windows Search",
        "Change Windows Search startup from Automatic to Manual. The currently running service is not stopped. " +
        "After a restart it will not start solely because of automatic startup; Windows or an application may still request it.",
        "File, email and Start-menu search can be slower or have incomplete/outdated indexed results while indexing is not running. " +
        "No immediate or future RAM savings are guaranteed. Restore reverses the configuration, without starting or stopping the service.",
        ServiceStartupType.Manual);

    public static SupportedBackgroundOptimization Resolve(string id) => id == SearchManualId ? SearchManual
        : throw new InvalidOperationException("This background action is not supported.");

    public static SupportedBackgroundOptimization? ForService(WindowsServiceInfo service) =>
        service.ServiceName == SearchManual.ServiceName && service.IsAccessible && service.StartType == "Auto"
            ? SearchManual : null;
}
