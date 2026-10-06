using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using WinOptimizer.Models;

namespace WinOptimizer.Services;

// Machine-wide, admin-writable history also works when UAC uses a different administrator account.
// Existing Privacy/Startup histories are never opened or overwritten by this store.
internal sealed class BackgroundServiceHistoryStore : IBackgroundServiceHistoryStore
{
    private readonly string _directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "WinOptimizer.BackgroundHistory");
    private static readonly SecurityIdentifier Administrators = new(WellKnownSidType.BuiltinAdministratorsSid, null);
    private static readonly SecurityIdentifier SystemSid = new(WellKnownSidType.LocalSystemSid, null);
    private static readonly SecurityIdentifier Users = new(WellKnownSidType.BuiltinUsersSid, null);

    public IDisposable AcquireWriteLock()
    {
        if (!AdminService.IsRunningAsAdministrator()) throw new UnauthorizedAccessException("History writes require elevation.");
        if (!Directory.Exists(_directory))
        {
            var security = new DirectorySecurity();
            security.SetAccessRuleProtection(true, false);
            security.SetOwner(Administrators);
            foreach (var sid in new[] { Administrators, SystemSid, Users })
                security.AddAccessRule(new FileSystemAccessRule(sid, sid == Users ? FileSystemRights.ReadAndExecute : FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            new DirectoryInfo(_directory).Create(security);
        }
        ValidateDirectory();
        string path = Path.Combine(_directory, "writer.lock");
        ValidateFileIfPresent(path);
        // Exclusive sharing serializes apply and restore, including separate elevated instances.
        return CreateHistoryFile(path, FileMode.OpenOrCreate);
    }

    public IReadOnlyList<BackgroundServiceHistoryEntry> Load()
    {
        if (!Directory.Exists(_directory)) return [];
        ValidateDirectory();
        return Directory.EnumerateFiles(_directory, "*.json").Select(path =>
        {
            if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "D", out var id))
                throw new InvalidDataException("Unexpected background history filename.");
            return Get(id);
        }).OrderByDescending(e => e.Timestamp).ToArray();
    }

    public BackgroundServiceHistoryEntry Get(Guid id)
    {
        if (id == Guid.Empty) throw new InvalidDataException("Invalid history identifier.");
        ValidateDirectory();
        string path = Path.Combine(_directory, $"{id:D}.json");
        ValidateFileIfPresent(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        var entry = JsonSerializer.Deserialize<BackgroundServiceHistoryEntry>(stream)
            ?? throw new InvalidDataException("Empty background history record.");
        if (entry.Id != id) throw new InvalidDataException("Background history identifier mismatch.");
        return entry;
    }

    public void Save(BackgroundServiceHistoryEntry entry)
    {
        if (!AdminService.IsRunningAsAdministrator()) throw new UnauthorizedAccessException("History writes require elevation.");
        ValidateDirectory();
        if (entry.Id == Guid.Empty) throw new InvalidDataException("Invalid history identifier.");
        string path = Path.Combine(_directory, $"{entry.Id:D}.json");
        ValidateFileIfPresent(path);
        string temp = Path.Combine(_directory, $"{Guid.NewGuid():D}.tmp");
        try
        {
            using (var stream = CreateHistoryFile(temp, FileMode.CreateNew))
            {
                JsonSerializer.Serialize(stream, entry, new JsonSerializerOptions { WriteIndented = true });
                stream.Flush(true);
            }
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private void ValidateDirectory()
    {
        var directory = new DirectoryInfo(_directory);
        if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Background history cannot use a redirected directory.");
        ValidateSecurity(directory.GetAccessControl());
    }
    private static void ValidateFileIfPresent(string path)
    {
        if (!File.Exists(path)) return;
        var file = new FileInfo(path);
        if ((file.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Redirected history files are not supported.");
        ValidateSecurity(file.GetAccessControl());
    }
    private static FileStream CreateHistoryFile(string path, FileMode mode) => new FileInfo(path).Create(mode,
        FileSystemRights.Read | FileSystemRights.Write, FileShare.None, 4096, FileOptions.None, CreateFileSecurity());

    internal static FileSecurity CreateFileSecurity()
    {
        var security = new FileSecurity();
        security.SetAccessRuleProtection(true, false);
        security.SetOwner(Administrators);
        foreach (var sid in new[] { Administrators, SystemSid, Users })
            security.AddAccessRule(new FileSystemAccessRule(sid,
                sid == Users ? FileSystemRights.Read : FileSystemRights.FullControl, AccessControlType.Allow));
        return security;
    }

    internal static void ValidateSecurity(FileSystemSecurity security)
    {
        var owner = security.GetOwner(typeof(SecurityIdentifier));
        if (!Administrators.Equals(owner) && !SystemSid.Equals(owner))
            throw new UnauthorizedAccessException("Background history must be owned by Administrators or SYSTEM. Existing permissions are not changed.");
        const FileSystemRights writes = FileSystemRights.Write | FileSystemRights.Delete | FileSystemRights.DeleteSubdirectoriesAndFiles |
            FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership;
        foreach (FileSystemAccessRule rule in security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            if (rule.AccessControlType == AccessControlType.Allow && (rule.FileSystemRights & writes) != 0 &&
                !Administrators.Equals(rule.IdentityReference) && !SystemSid.Equals(rule.IdentityReference))
                throw new UnauthorizedAccessException("Background history has untrusted write permissions. No service configuration was changed.");
    }
}
