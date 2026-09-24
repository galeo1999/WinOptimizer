using Microsoft.Win32;
using WinOptimizer.Models;
using System.Linq;

namespace WinOptimizer.Services;

public class StartupManager
{
    private readonly StartupHistoryManager
        _historyManager;

    public StartupManager()
    {
        _historyManager =
            new StartupHistoryManager();
    }

    public void Disable(
        StartupItem item)
    {
        EnsureSupported(item);

        if (item.State ==
            StartupState.Disabled)
        {
            throw new InvalidOperationException(
                $"'{item.Name}' is already disabled."
            );
        }

        EnsureAdmin(item);

        StartupHistoryEntry history =
            CreateHistoryEntry(
                item,
                "Disable"
            );

        _historyManager.Add(history);

        try
        {
            RegistryKey hive =
                GetHive(
                    item.StartupApprovedHive
                );

            using RegistryKey key =
                hive.CreateSubKey(
                    item.StartupApprovedPath,
                    writable: true
                );

            byte[] disabledValue =
                CreateDisabledValue();

            key.SetValue(
                item.StartupApprovedValueName,
                disabledValue,
                RegistryValueKind.Binary
            );

            history.Status = "Applied";

            _historyManager.Update(history);
        }
        catch
        {
            RestoreBackup(
                history.Backup
            );

            history.Status =
                "RolledBack";

            _historyManager.Update(history);

            throw;
        }
    }

    public void Enable(
        StartupItem item)
    {
        EnsureSupported(item);

        if (item.State ==
            StartupState.Enabled)
        {
            throw new InvalidOperationException(
                $"'{item.Name}' is already enabled."
            );
        }

        EnsureAdmin(item);

        StartupHistoryEntry history =
            CreateHistoryEntry(
                item,
                "Enable"
            );

        _historyManager.Add(history);

        try
        {
            RegistryKey hive =
                GetHive(
                    item.StartupApprovedHive
                );

            using RegistryKey? key =
                hive.OpenSubKey(
                    item.StartupApprovedPath,
                    writable: true
                );

            /*
             * Der eigentliche Run-Eintrag bleibt bestehen.
             * Wir entfernen nur den expliziten
             * StartupApproved-Status.
             *
             * Ein fehlender Approval-Wert wird von Windows
             * bei klassischen Run-Einträgen normalerweise
             * als nicht explizit deaktiviert behandelt.
             */
            key?.DeleteValue(
                item.StartupApprovedValueName,
                throwOnMissingValue: false
            );

            history.Status =
                "Applied";

            _historyManager.Update(history);
        }
        catch
        {
            RestoreBackup(
                history.Backup
            );

            history.Status =
                "RolledBack";

            _historyManager.Update(history);

            throw;
        }
    }

    private StartupHistoryEntry CreateHistoryEntry(
        StartupItem item,
        string action)
    {
        RegistryKey hive =
            GetHive(
                item.StartupApprovedHive
            );

        using RegistryKey? key =
            hive.OpenSubKey(
                item.StartupApprovedPath
            );

        object? currentValue =
            key?.GetValue(
                item.StartupApprovedValueName
            );

        StartupBackup backup =
            new StartupBackup
            {
                Hive =
                    item.StartupApprovedHive,

                Path =
                    item.StartupApprovedPath,

                Name =
                    item.StartupApprovedValueName
            };

        if (currentValue is byte[] bytes)
        {
            backup.Existed = true;

            backup.BinaryValueBase64 =
                Convert.ToBase64String(
                    bytes
                );
        }
        else
        {
            backup.Existed = false;
        }

        return new StartupHistoryEntry
        {
            StartupName =
                item.Name,

            Action =
                action,

            Backup =
                backup
        };
    }

    private static byte[]
        CreateDisabledValue()
    {
        byte[] data =
            new byte[12];

        /*
         * 03 is the commonly observed disabled
         * StartupApproved state.
         */
        data[0] = 0x03;

        long fileTime =
            DateTime.UtcNow
                .ToFileTimeUtc();

        byte[] timeBytes =
            BitConverter.GetBytes(
                fileTime
            );

        Array.Copy(
            timeBytes,
            0,
            data,
            4,
            8
        );

        return data;
    }

    private static void RestoreBackup(
        StartupBackup backup)
    {
        RegistryKey hive =
            GetHive(
                backup.Hive
            );

        if (!backup.Existed)
        {
            using RegistryKey? key =
                hive.OpenSubKey(
                    backup.Path,
                    writable: true
                );

            key?.DeleteValue(
                backup.Name,
                false
            );

            return;
        }

        if (string.IsNullOrWhiteSpace(
            backup.BinaryValueBase64))
        {
            throw new InvalidOperationException(
                "Startup backup contains no binary value."
            );
        }

        byte[] bytes =
            Convert.FromBase64String(
                backup.BinaryValueBase64
            );

        using RegistryKey restoreKey =
            hive.CreateSubKey(
                backup.Path,
                writable: true
            );

        restoreKey.SetValue(
            backup.Name,
            bytes,
            RegistryValueKind.Binary
        );
    }

  private static void EnsureSupported(
    StartupItem item)
{
    bool supported =
        item.SourceType ==
            StartupSourceType.Registry ||
        item.SourceType ==
            StartupSourceType.StartupFolder;

    if (!supported)
    {
        throw new NotSupportedException(
            "This startup source is not supported for modification."
        );
    }

    if (item.State ==
        StartupState.Unknown)
    {
        throw new InvalidOperationException(
            "Startup state is unknown."
        );
    }

    if (string.IsNullOrWhiteSpace(
            item.StartupApprovedHive) ||
        string.IsNullOrWhiteSpace(
            item.StartupApprovedPath) ||
        string.IsNullOrWhiteSpace(
            item.StartupApprovedValueName))
    {
        throw new InvalidOperationException(
            "StartupApproved information is incomplete."
        );
    }
}

    private static void EnsureAdmin(
        StartupItem item)
    {
        if (item.RequiresAdmin &&
            !AdminService
                .IsRunningAsAdministrator())
        {
            throw new UnauthorizedAccessException(
                "Administrator privileges are required."
            );
        }
    }

    private static RegistryKey GetHive(
        string hive)
    {
        return hive switch
        {
            "CurrentUser" =>
                Registry.CurrentUser,

            "LocalMachine" =>
                Registry.LocalMachine,

            _ =>
                throw new NotSupportedException(
                    $"Registry hive '{hive}' is unsupported."
                )
        };
    }
    public List<StartupHistoryEntry> GetHistory()
    {
        return _historyManager
            .LoadHistory()
            .OrderByDescending(
                entry => entry.CreatedAt
            )
            .ToList();
    }
    public bool CanRestore(
        string historyId)
    {
        List<StartupHistoryEntry> history =
            _historyManager.LoadHistory();

        StartupHistoryEntry? entry =
            history.FirstOrDefault(
                item => item.Id == historyId
            );

        if (entry == null)
        {
            return false;
        }

        if (entry.Status != "Applied")
        {
            return false;
        }

        StartupHistoryEntry? newestApplied =
            history
                .Where(item =>
                    item.Status == "Applied" &&
                    IsSameStartupEntry(
                        item,
                        entry
                    )
                )
                .OrderByDescending(
                    item => item.CreatedAt
                )
                .FirstOrDefault();

        return newestApplied?.Id ==
               entry.Id;
    }
    public void Restore(
    string historyId)
    {
        if (!CanRestore(historyId))
        {
            throw new InvalidOperationException(
                "This startup history entry cannot be restored."
            );
        }

        StartupHistoryEntry entry =
            _historyManager.GetById(
                historyId
            )
            ?? throw new InvalidOperationException(
                "Startup history entry was not found."
            );

        RestoreBackup(
            entry.Backup
        );

        entry.Status =
            "Restored";

        entry.RestoredAt =
            DateTimeOffset.Now;

        _historyManager.Update(
            entry
        );
    }
    private static bool IsSameStartupEntry(
    StartupHistoryEntry left,
    StartupHistoryEntry right)
{
    return
        string.Equals(
            left.Backup.Hive,
            right.Backup.Hive,
            StringComparison.OrdinalIgnoreCase
        ) &&

        string.Equals(
            left.Backup.Path,
            right.Backup.Path,
            StringComparison.OrdinalIgnoreCase
        ) &&

        string.Equals(
            left.Backup.Name,
            right.Backup.Name,
            StringComparison.OrdinalIgnoreCase
        );
}
}