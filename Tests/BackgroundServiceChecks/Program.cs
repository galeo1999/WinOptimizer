using System.Text.Json;
using System.Security.AccessControl;
using System.Security.Principal;
using WinOptimizer.Models;
using WinOptimizer.Services;

// No live writes: every transaction uses this in-memory adapter. --read-live only queries WSearch.
internal static class Program
{
    private static int _checks;
    private static readonly string Id = SupportedBackgroundActions.SearchManualId;
    private static void Check(bool value, string message)
    { if (!value) throw new Exception(message); _checks++; }
    private static void Reject(Action action, string message)
    { try { action(); } catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException) { _checks++; return; } throw new Exception(message); }
    private static (BackgroundServiceManager Manager, FakeAccess Access, FakeHistory History) Create(bool delayed = true, bool admin = true)
    {
        var history = new FakeHistory();
        var access = new FakeAccess { Configuration = new(ServiceStartupType.Automatic, delayed, 4, @"C:\Windows\System32\SearchIndexer.exe /Embedding", 16) };
        access.BeforeWrite = () => Check(history.Entries.Count > 0, "Write occurred without durable backup");
        return (new(access, history, () => admin), access, history);
    }
    private static void Main(string[] args)
    {
        foreach (string id in new[] { "WSearch", "wuauserv", "WinDefend", "RpcSs", "EventLog", "Schedule", "MpsSvc", "windows-search-manual --service RpcSs", "", "../history" })
            Reject(() => SupportedBackgroundActions.Resolve(id), "Arbitrary action accepted");
        Check(SupportedBackgroundActions.ForService(new() { ServiceName = "WSearch", StartType = "Auto", IsAccessible = true }) != null, "Supported action missing");
        foreach (var service in new[] { new WindowsServiceInfo { ServiceName="WSearch", StartType="Manual", IsAccessible=true },
            new WindowsServiceInfo { ServiceName="WSearch", StartType="Auto", IsAccessible=false }, new WindowsServiceInfo { ServiceName="WinDefend", StartType="Auto", IsAccessible=true } })
            Check(SupportedBackgroundActions.ForService(service)==null, "Unsupported UI action exposed");
        foreach (bool delayed in new[] { false, true })
        {
            var (manager, access, history) = Create(delayed);
            var original = access.Configuration;
            var plan = manager.PreviewApply(Id);
            Check(access.Writes==0 && history.Entries.Count==0, "Preview modified state");
            var applied = manager.ExecuteConfirmed(plan);
            var saved = history.Get(applied.HistoryId);
            Check(applied.Success && saved.Status=="Applied" && saved.PreviousConfiguration==original, "Backup or Applied state incorrect");
            Check(access.Configuration.StartupType==ServiceStartupType.Manual && !access.Configuration.DelayedAutoStart && access.Configuration.RunningState==4, "Apply affected running state");
            // Service can change running state independently; restore must not force the saved state.
            access.Configuration = access.Configuration with { RunningState=1 };
            var restored = manager.ExecuteConfirmed(manager.PreviewRestore(applied.HistoryId));
            Check(restored.Success && access.Configuration.SameSettings(original) && access.Configuration.RunningState==1, "Exact startup restore failed or running state changed");
            Check(history.Get(applied.HistoryId).Status=="Restored" && history.Get(applied.HistoryId).RestoreStatus=="Restored", "Restore history incorrect");
            Reject(() => manager.PreviewRestore(applied.HistoryId), "Repeated restore accepted");
        }
        {
            var (manager,access,history)=Create(admin:false);
            Reject(()=>manager.ExecuteConfirmed(manager.PreviewApply(Id)),"Unprivileged mutation accepted");
            Check(access.Writes==0 && history.Entries.Count==0,"Unprivileged request wrote state");
        }
        {
            var (manager,access,history)=Create(); var plan=manager.PreviewApply(Id);
            access.Configuration=access.Configuration with { DelayedAutoStart=false };
            Reject(()=>manager.ExecuteConfirmed(plan),"Stale confirmation accepted");
            Check(access.Writes==0 && history.Entries.Count==0,"Stale plan mutated state");
            access.Configuration=access.Configuration with { StartupType=ServiceStartupType.Manual };
            Reject(()=>manager.PreviewApply(Id),"Manual entry accepted");
        }
        {
            var (manager,access,_)=Create();var plan=manager.PreviewApply(Id);
            Reject(()=>manager.ExecuteConfirmed(new(plan.Action with { ServiceName="RpcSs" },plan.Current,plan.Target,null)),"Forged service name accepted");
            Reject(()=>manager.ExecuteConfirmed(new(plan.Action,plan.Current,plan.Target with { StartupType=ServiceStartupType.Disabled },null)),"Forged target accepted");
            Check(access.Writes==0,"Forged plans wrote state");
        }
        {
            var (manager,access,history)=Create(); history.FailSaveNumber=1;
            Reject(()=>manager.ExecuteConfirmed(manager.PreviewApply(Id)),"Backup failure not propagated");
            Check(access.Writes==0,"Changed service after backup failure");
        }
        foreach (bool throwAfterWrite in new[]{false,true})
        {
            var (manager,access,history)=Create();var original=access.Configuration;
            access.IgnoreWriteNumber=throwAfterWrite ? 0 : 1; access.ThrowWriteNumber=throwAfterWrite ? 1 : 0;
            var result=manager.ExecuteConfirmed(manager.PreviewApply(Id));
            Check(!result.Success && access.Configuration.SameSettings(original),"Verification/write failure did not roll back");
            Check(history.Get(result.HistoryId).Status=="Failed" && history.Get(result.HistoryId).RollbackStatus=="Previous configuration verified","Failed/rollback state incorrect");
        }
        {
            var (manager,access,history)=Create();access.ThrowWriteNumber=1;access.FailRollback=true;
            var result=manager.ExecuteConfirmed(manager.PreviewApply(Id));
            Check(!result.Success && history.Get(result.HistoryId).RollbackStatus!.StartsWith("Rollback failed"),"Rollback failure hidden");
        }
        {
            var (manager,access,history)=Create();var original=access.Configuration;history.FailSaveNumber=2;
            var result=manager.ExecuteConfirmed(manager.PreviewApply(Id));
            Check(!result.Success && access.Configuration.SameSettings(original) && history.Get(result.HistoryId).Status=="Failed","Final history failure not rolled back");
        }
        {
            var (manager,access,history)=Create(); var result=manager.ExecuteConfirmed(manager.PreviewApply(Id));
            var manual=access.Configuration;access.IgnoreWriteNumber=2;
            var restore=manager.ExecuteConfirmed(manager.PreviewRestore(result.HistoryId));
            Check(!restore.Success && access.Configuration.SameSettings(manual),"Failed restore did not retain original applied state");
            Check(history.Get(result.HistoryId).RestoreStatus=="Failed" && history.Get(result.HistoryId).RestoredAt==null,"Failed restore falsely marked restored");
        }
        {
            var (manager,access,history)=Create();var result=manager.ExecuteConfirmed(manager.PreviewApply(Id));
            var entry=history.Get(result.HistoryId);entry.ServiceName="WinDefend";history.Save(entry);
            int writes=access.Writes;Reject(()=>manager.PreviewRestore(result.HistoryId),"Tampered restore name accepted");
            Check(access.Writes==writes,"Tampered restore wrote state");
            entry.ServiceName="WSearch";entry.PreviousConfiguration=entry.PreviousConfiguration with { StartupType=(ServiceStartupType)0 };history.Save(entry);
            Reject(()=>manager.PreviewRestore(result.HistoryId),"Boot-start payload accepted");
        }
        {
            var (manager,access,history)=Create();var result=manager.ExecuteConfirmed(manager.PreviewApply(Id));
            var entry=history.Get(result.HistoryId);entry.Status="Pending";history.Save(entry);
            access.Configuration=entry.PreviousConfiguration with { DelayedAutoStart=false };
            Check(manager.ExecuteConfirmed(manager.PreviewRestore(result.HistoryId)).Success && access.Configuration.DelayedAutoStart,"Interrupted transition cannot be recovered");
        }
        {
            var (manager,access,history)=Create();var result=manager.ExecuteConfirmed(manager.PreviewApply(Id));
            var newer=history.Get(result.HistoryId);newer.Id=Guid.NewGuid();newer.Timestamp=newer.Timestamp.AddSeconds(1);history.Save(newer);
            Reject(()=>manager.PreviewRestore(result.HistoryId),"Older restore bypassed newer change");
            access.Configuration=access.Configuration with { StartupType=ServiceStartupType.Disabled };
            Reject(()=>manager.PreviewRestore(newer.Id),"External configuration overwritten");
        }
        foreach (bool admin in new[] { false, true })
        {
            var (manager, access, history) = Create(admin:admin);
            int elevationCalls = 0;
            var gate = new BackgroundServiceActionCoordinator(manager, _ => false, () => admin,
                (_, _) => { elevationCalls++; return Task.FromResult<int?>(0); }, (_, _) => { });
            Check(gate.RunAsync(Id, null).GetAwaiter().GetResult() == 2 && access.Writes == 0 && history.Entries.Count == 0 && elevationCalls == 0,
                "Confirmation cancellation allowed elevation or writes");
        }
        foreach (int? exit in new int?[] { null, 2, 1, 0 })
        {
            var (manager, access, history) = Create(admin:false);
            int confirmations=0;
            var gate=new BackgroundServiceActionCoordinator(manager, _ => { confirmations++; return true; }, () => false,
                (actionId, restoreId) => { Check(actionId==Id && restoreId==null,"Elevation payload not a catalog ID");return Task.FromResult(exit); }, (_, _) => { });
            int result=gate.RunAsync(Id,null).GetAwaiter().GetResult();
            Check(result==(exit==null || exit==2 ? 2 : exit==0 ? 0 : 1) && confirmations==1 && access.Writes==0 && history.Entries.Count==0,
                "UAC result handling caused local mutations or incorrect status");
        }
        {
            var (manager,access,history)=Create();int confirmations=0;
            var gate=new BackgroundServiceActionCoordinator(manager,_=>{confirmations++;return true;},()=>true,
                (_,_)=>throw new Exception("Elevated operation requested another UAC"),(_,_)=>{});
            Check(gate.RunAsync(Id,null,true).GetAwaiter().GetResult()==0 && confirmations==1 && history.Entries.Values.Any(),"Elevated command skipped confirmation");
        }
        {
            var security=BackgroundServiceHistoryStore.CreateFileSecurity();
            BackgroundServiceHistoryStore.ValidateSecurity(security); Check(true,"Generated history ACL accepted");
            security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid,null),FileSystemRights.Write,AccessControlType.Allow));
            Reject(()=>BackgroundServiceHistoryStore.ValidateSecurity(security),"Untrusted history write ACL accepted");
            security=BackgroundServiceHistoryStore.CreateFileSecurity();
            security.SetOwner(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid,null));
            Reject(()=>BackgroundServiceHistoryStore.ValidateSecurity(security),"Untrusted history owner accepted");
        }
        {
            var (manager,access,history)=Create();var result=manager.ExecuteConfirmed(manager.PreviewApply(Id));
            var entry=history.Get(result.HistoryId);entry.RestoreStatus="Pending";history.Save(entry);
            access.Configuration=entry.PreviousConfiguration with { DelayedAutoStart=false };
            Check(manager.ExecuteConfirmed(manager.PreviewRestore(result.HistoryId)).Success && access.Configuration.DelayedAutoStart,"Interrupted restore cannot recover delayed start");
        }
        {
            var (manager,access,history)=Create();access.ThrowWriteNumber=1;access.ExternalChangeOnFailure=true;
            var result=manager.ExecuteConfirmed(manager.PreviewApply(Id));
            Check(!result.Success && access.Writes==1 && access.Configuration.StartupType==ServiceStartupType.Disabled &&
                history.Get(result.HistoryId).RollbackStatus!.StartsWith("Rollback skipped"),"Rollback overwrote external configuration");
        }
        var action=SupportedBackgroundActions.Resolve(Id);
        foreach (string binary in new[] { @"C:\Unrelated\SearchIndexer.exe", @"C:\Windows\System32\svchost.exe", "SearchIndexer.exe", @"""C:\Windows\System32\SearchIndexer.exe""evil" })
            Reject(()=>WindowsServiceConfigurationAccess.ValidateIdentity(action,new(ServiceStartupType.Automatic,false,4,binary,16)),"Unexpected executable accepted");
        if(args.Contains("--read-live"))
        {
            var configuration=new WindowsServiceConfigurationAccess().Read(action);
            Console.WriteLine($"Read-only native query: WSearch {configuration.StartupDescription}; elevated={AdminService.IsRunningAsAdministrator()}. No native writes performed.");
            Check(configuration.ServiceType==16,"Native service query invalid");
        }
        Console.WriteLine($"Passed {_checks} service-action checks. All mutation tests used fake services and history.");
    }
}

internal sealed class FakeAccess : IServiceConfigurationAccess
{
    public ServiceConfiguration Configuration = null!;
    public int Writes, IgnoreWriteNumber, ThrowWriteNumber;
    public bool FailRollback, ExternalChangeOnFailure;
    public Action? BeforeWrite;
    public ServiceConfiguration Read(SupportedBackgroundOptimization action) => Configuration;
    public void Write(SupportedBackgroundOptimization action, ServiceConfiguration target)
    {
        BeforeWrite?.Invoke(); Writes++;
        if(FailRollback && Writes>1) throw new InvalidOperationException("Injected rollback failure");
        if(Writes==IgnoreWriteNumber)return;
        Configuration=target with { RunningState=Configuration.RunningState };
        if(Writes==ThrowWriteNumber) { if(ExternalChangeOnFailure) Configuration=Configuration with {StartupType=ServiceStartupType.Disabled}; throw new InvalidOperationException("Injected write failure"); }
    }
}
internal sealed class FakeHistory : IBackgroundServiceHistoryStore
{
    public Dictionary<Guid,string> Entries=new();
    public int Saves, FailSaveNumber;
    public IDisposable AcquireWriteLock()=>new Lease();
    public IReadOnlyList<BackgroundServiceHistoryEntry> Load()=>Entries.Keys.Select(Get).ToArray();
    public BackgroundServiceHistoryEntry Get(Guid id)=>JsonSerializer.Deserialize<BackgroundServiceHistoryEntry>(Entries[id])!;
    public void Save(BackgroundServiceHistoryEntry entry)
    { if(++Saves==FailSaveNumber)throw new InvalidOperationException("Injected durable-history failure");Entries[entry.Id]=JsonSerializer.Serialize(entry); }
    private sealed class Lease : IDisposable { public void Dispose(){} }
}
