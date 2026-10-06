# Background service action checks

Run on Windows with the existing .NET 10 SDK; no test packages are needed:

```powershell
dotnet run --project Tests/BackgroundServiceChecks/BackgroundServiceChecks.csproj
```

All mutation tests use an in-memory service and history adapter. They exercise the production manager and confirmation/elevation coordinator, including allowlist validation, forged IDs/targets, backup failures, verification, rollback, cancellation, simulated UAC results, and delayed-start restoration. They never call the native write adapter.

Optional standard-user native API read check (requires Windows Search to be installed):

```powershell
dotnet run --project Tests/BackgroundServiceChecks/BackgroundServiceChecks.csproj -- --read-live
```

This only reads WSearch configuration. It does not apply any action.

Manual integration checks in a disposable Windows VM:

1. In Background → Opportunities, verify only Automatic Windows Search has Review Change. Other services, Manual/Disabled Search, and unavailable metadata must remain informational.
2. Review the current/proposed type and consequences. Cancel: no history or service change. Confirm, then cancel UAC: no change.
3. Approve UAC; cancel the fresh elevated confirmation: no change. Confirm it: verify Manual in Services and Applied in Restore → Background Changes. Verify the process was not stopped by WinOptimizer and the original window remains non-elevated.
4. Restore, including UAC. Verify the exact original Automatic / Automatic (Delayed Start) setting and Restored status. Restore must not start a service that stopped independently.
5. Try an alternate administrator credential: machine-wide history must remain visible to the original account. Verify standard users cannot edit history files.
6. Check existing Startup and Privacy restore, Background refresh, and all Performance tabs.

History lives in `%ProgramData%\WinOptimizer.BackgroundHistory`. New history objects are protected for administrator/SYSTEM writes and user reads; existing untrusted permissions or reparse points are rejected rather than repaired. A durable Pending record precedes changes. Interrupted changes remain reviewable in Restore. A newer unresolved change or unrelated external configuration prevents an older restore from overwriting it.

Windows Search indexing affects file and email search: https://learn.microsoft.com/en-us/troubleshoot/windows-client/shell-experience/windows-search-performance-issues
The implementation uses documented service configuration APIs, including the separate delayed-start flag: https://learn.microsoft.com/en-us/windows/win32/api/winsvc/ns-winsvc-service_delayed_auto_start_info

## Files in this change

Added:

- `App.Background.cs`
- `Models/BackgroundServiceChange.cs`
- `Services/AdminService.Background.cs`
- `Services/BackgroundServiceActionCoordinator.cs`
- `Services/BackgroundServiceHistoryStore.cs`
- `Services/BackgroundServiceManager.cs`
- `Services/SupportedBackgroundActions.cs`
- `Services/WindowsServiceConfigurationAccess.cs`
- `Tests/BackgroundServiceChecks/BackgroundServiceChecks.csproj`
- `Tests/BackgroundServiceChecks/Program.cs`
- `Tests/BackgroundServiceChecks/README.md`
- `Views/BackgroundServiceActionWorkflow.cs`
- `Views/BackgroundServiceConfirmation.xaml`
- `Views/BackgroundServiceConfirmation.xaml.cs`
- `Views/RestoreView.Background.xaml.cs`

Modified:

- `App.xaml.cs`
- `Models/BackgroundOptimizationRecommendation.cs`
- `Services/AdminService.cs`
- `Services/BackgroundOptimizationAdvisor.cs`
- `Views/PerformanceView.BackgroundAdvisor.xaml.cs`
- `Views/PerformanceView.xaml`
- `Views/RestoreView.xaml`
- `Views/RestoreView.xaml.cs`
- `WinOptimizer.csproj`
