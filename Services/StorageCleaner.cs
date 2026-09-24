using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using WinOptimizer.Models;

namespace WinOptimizer.Services;

public class StorageCleaner
{
    public Task<StorageCleanupResult> CleanAsync(
        StorageLocationInfo location,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(location);
        // Snapshot and validate before dispatching: candidate flags never authorize a path.
        StorageLocationType type = location.Type;
        string root = type switch
        {
            StorageLocationType.UserTemp => Normalize(Path.GetTempPath()),
            StorageLocationType.WindowsTemp => Normalize(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp")),
            StorageLocationType.RecycleBin => "",
            _ => throw new NotSupportedException("This storage location cannot be cleaned.")
        };

        if (!location.IsCleanupCandidate)
            throw new InvalidOperationException("This storage location is not a cleanup candidate.");

        if (type != StorageLocationType.RecycleBin)
        {
            if (!Path.IsPathFullyQualified(location.Path) ||
                !string.Equals(Normalize(location.Path), root, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(root, Path.GetPathRoot(root), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Cleanup is restricted to the known temporary directory.");

            string downloads = Normalize(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));
            // Fail closed if TEMP has been redirected to Downloads or one of its ancestors/children.
            if (IsWithin(root, downloads) || IsWithin(downloads, root))
                throw new InvalidOperationException("Downloads cannot be cleaned.");
        }

        if (type == StorageLocationType.WindowsTemp && !AdminService.IsRunningAsAdministrator())
            throw new UnauthorizedAccessException("Administrator privileges are required to clean Windows Temp.");

        return Task.Run(() => type == StorageLocationType.RecycleBin
            ? EmptyRecycleBin()
            : CleanTempDirectory(root, cancellationToken), cancellationToken);
    }

    private static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static bool IsWithin(string path, string root) =>
        string.Equals(path, root, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);

    private static StorageCleanupResult CleanTempDirectory(string root, CancellationToken token)
    {
        var result = new StorageCleanupResult();
        DateTime cutoff = DateTime.UtcNow.AddHours(-24);
        var directories = new Stack<(string Path, bool Remove)>();
        directories.Push((root, false));

        while (directories.TryPop(out var entry))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                if (!IsWithin(entry.Path, root))
                    throw new InvalidOperationException("A cleanup path escaped the temporary directory.");

                // Pin every ancestor against renaming/replacement, and inspect each opened
                // directory without following reparse points. Keep these handles while using paths.
                using var guard = new DirectoryGuard(entry.Remove
                    ? Path.GetDirectoryName(entry.Path)! : entry.Path);
                if (entry.Remove)
                {
                    if ((File.GetAttributes(entry.Path) & FileAttributes.ReparsePoint) == 0)
                        Directory.Delete(entry.Path, recursive: false);
                    continue;
                }

                if (!string.Equals(entry.Path, root, StringComparison.OrdinalIgnoreCase))
                    directories.Push((entry.Path, true));

                foreach (string child in Directory.EnumerateFileSystemEntries(entry.Path))
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        FileAttributes attributes = File.GetAttributes(child);
                        if ((attributes & FileAttributes.ReparsePoint) != 0)
                            result.SkippedFiles++;
                        else if ((attributes & FileAttributes.Directory) != 0)
                            directories.Push((child, false));
                        else
                            DeleteOldFile(child, cutoff, result);
                    }
                    catch (Exception ex) when (IsSkippable(ex))
                    {
                        result.SkippedFiles++;
                    }
                }
            }
            catch (Exception ex) when (IsSkippable(ex))
            {
                // Nonempty directories are expected when recent/locked files remain.
                if (!entry.Remove)
                    result.SkippedFiles++;
            }
        }
        return result;
    }

    private static bool IsSkippable(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or Win32Exception;

    private static void DeleteOldFile(string path, DateTime cutoff, StorageCleanupResult result)
    {
        // Exclusive access skips files in use. Delete the inspected handle itself so a
        // replacement file cannot be deleted between the age check and deletion.
        using SafeFileHandle handle = CreateFile(path, 0x00010080, 0, IntPtr.Zero, 3,
            0x00200000, IntPtr.Zero); // DELETE | READ_ATTRIBUTES; OPEN_REPARSE_POINT
        if (handle.IsInvalid)
            throw new Win32Exception(Marshal.GetLastWin32Error());
        FileInformation info = GetInformation(handle);
        if ((info.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0 ||
            ToUtc(info.CreationTime) >= cutoff || ToUtc(info.LastWriteTime) >= cutoff)
        {
            result.SkippedFiles++;
            return;
        }

        int delete = 1;
        if (!SetFileInformationByHandle(handle, 4, ref delete, sizeof(int)))
        {
            int error = Marshal.GetLastWin32Error();
            if (error is 5 or 32 or 33) // Access denied / sharing or lock violation
                result.SkippedFiles++;
            else
                result.FailedFiles++;
            return;
        }
        result.DeletedFiles++;
        result.DeletedBytes += ((long)info.SizeHigh << 32) | info.SizeLow;
    }

    private static DateTime ToUtc(System.Runtime.InteropServices.ComTypes.FILETIME time) =>
        DateTime.FromFileTimeUtc(((long)time.dwHighDateTime << 32) | (uint)time.dwLowDateTime);

    private static FileInformation GetInformation(SafeFileHandle handle)
    {
        if (!GetFileInformationByHandle(handle, out FileInformation info))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return info;
    }

    private sealed class DirectoryGuard : IDisposable
    {
        private readonly List<SafeFileHandle> _handles = [];

        public DirectoryGuard(string path)
        {
            try
            {
                var ancestors = new Stack<string>();
                for (DirectoryInfo? dir = new(path); dir != null; dir = dir.Parent)
                    ancestors.Push(dir.FullName);
                foreach (string ancestor in ancestors)
                {
                    SafeFileHandle handle = CreateFile(ancestor, 0x80, 3, IntPtr.Zero, 3,
                        0x02200000, IntPtr.Zero); // BACKUP_SEMANTICS | OPEN_REPARSE_POINT; no delete sharing
                    _handles.Add(handle);
                    if (handle.IsInvalid)
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    FileAttributes attributes = GetInformation(handle).Attributes;
                    if ((attributes & FileAttributes.ReparsePoint) != 0 ||
                        (attributes & FileAttributes.Directory) == 0)
                        throw new IOException("Reparse points are excluded from cleanup.");
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            for (int i = _handles.Count - 1; i >= 0; i--)
                _handles[i].Dispose();
        }
    }

    private static StorageCleanupResult EmptyRecycleBin()
    {
        RecycleBinInformation before = QueryRecycleBin();
        if (before.Items == 0)
            return new StorageCleanupResult();

        // The view always obtains explicit confirmation before calling this method.
        int code = SHEmptyRecycleBin(IntPtr.Zero, null, 0x1 | 0x2 | 0x4);
        if (code != 0)
            throw new COMException("Windows could not completely empty the Recycle Bin.", code);
        RecycleBinInformation after = QueryRecycleBin();
        return new StorageCleanupResult
        {
            DeletedFiles = Math.Max(0, before.Items - after.Items),
            DeletedBytes = Math.Max(0, before.Size - after.Size),
            SkippedFiles = after.Items,
            IsEstimate = true
        };
    }

    private static RecycleBinInformation QueryRecycleBin()
    {
        var info = new RecycleBinInformation { StructureSize = Marshal.SizeOf<RecycleBinInformation>() };
        int code = SHQueryRecycleBin(null, ref info);
        if (code != 0)
            throw new COMException("Windows could not query the Recycle Bin.", code);
        return info;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        public FileAttributes Attributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime, LastAccessTime, LastWriteTime;
        public uint VolumeSerial, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    private struct RecycleBinInformation
    {
        public int StructureSize;
        public long Size, Items;
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint share,
        IntPtr security, uint disposition, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation info);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int informationClass,
        ref int information, int size);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? root, uint flags);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(string? root, ref RecycleBinInformation info);
}
