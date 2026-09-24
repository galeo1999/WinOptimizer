using System.Text.Json;
using Microsoft.Win32;
using WinOptimizer.Models;

namespace WinOptimizer.Services;

public class RegistryService
{
    public bool ValueMatches(
        RegistryChange change)
    {
        RegistryKey baseKey =
            GetRegistryHive(change.Hive);

        using RegistryKey? key =
            baseKey.OpenSubKey(change.Path);

        if (key == null)
        {
            return false;
        }

        object? currentValue =
            key.GetValue(change.Name);

        if (currentValue == null)
        {
            return false;
        }

        object expectedValue =
            ConvertExpectedValue(change);

        return currentValue.Equals(expectedValue);
    }

    public RegistryBackup CreateBackup(
        RegistryChange change)
    {
        RegistryKey baseKey =
            GetRegistryHive(change.Hive);

        using RegistryKey? key =
            baseKey.OpenSubKey(change.Path);

        if (key == null)
        {
            return CreateMissingBackup(change);
        }

        object? value =
            key.GetValue(
                change.Name,
                null,
                RegistryValueOptions.DoNotExpandEnvironmentNames
            );

        if (value == null)
        {
            return CreateMissingBackup(change);
        }

        RegistryValueKind kind =
            key.GetValueKind(change.Name);

        return new RegistryBackup
        {
            Hive = change.Hive,
            Path = change.Path,
            Name = change.Name,
            Existed = true,
            Type = kind.ToString(),

            Value =
                JsonSerializer.SerializeToElement(
                    value
                )
        };
    }

    public void Apply(
        RegistryChange change)
    {
        RegistryKey baseKey =
            GetRegistryHive(change.Hive);

        using RegistryKey key =
            baseKey.CreateSubKey(
                change.Path,
                writable: true
            );

        RegistryValueKind kind =
            GetRegistryValueKind(
                change.Type
            );

        object value =
            ConvertExpectedValue(change);

        key.SetValue(
            change.Name,
            value,
            kind
        );
    }

    public void Restore(
        RegistryBackup backup)
    {
        RegistryKey baseKey =
            GetRegistryHive(backup.Hive);

        // Der Wert existierte vorher nicht.
        if (!backup.Existed)
        {
            using RegistryKey? key =
                baseKey.OpenSubKey(
                    backup.Path,
                    writable: true
                );

            key?.DeleteValue(
                backup.Name,
                throwOnMissingValue: false
            );

            return;
        }

        if (backup.Value == null)
        {
            throw new InvalidOperationException(
                $"Backup value for '{backup.Name}' is missing."
            );
        }

        RegistryValueKind kind =
            Enum.Parse<RegistryValueKind>(
                backup.Type
            );

        object value =
            ConvertBackupValue(
                backup.Value.Value,
                kind
            );

        using RegistryKey restoreKey =
            baseKey.CreateSubKey(
                backup.Path,
                writable: true
            );

        restoreKey.SetValue(
            backup.Name,
            value,
            kind
        );
    }

    private static RegistryBackup CreateMissingBackup(
        RegistryChange change)
    {
        return new RegistryBackup
        {
            Hive = change.Hive,
            Path = change.Path,
            Name = change.Name,
            Existed = false
        };
    }

    private static RegistryKey GetRegistryHive(
        string hive)
    {
        return hive.ToLowerInvariant() switch
        {
            "currentuser" =>
                Registry.CurrentUser,

            "localmachine" =>
                Registry.LocalMachine,

            _ => throw new NotSupportedException(
                $"Registry hive '{hive}' is not supported."
            )
        };
    }

    private static RegistryValueKind GetRegistryValueKind(
        string type)
    {
        return type.ToLowerInvariant() switch
        {
            "dword" =>
                RegistryValueKind.DWord,

            "qword" =>
                RegistryValueKind.QWord,

            "string" =>
                RegistryValueKind.String,

            _ => throw new NotSupportedException(
                $"Registry type '{type}' is not supported."
            )
        };
    }

    private static object ConvertExpectedValue(
        RegistryChange change)
    {
        return change.Type.ToLowerInvariant() switch
        {
            "dword" =>
                change.Value.GetInt32(),

            "qword" =>
                change.Value.GetInt64(),

            "string" =>
                change.Value.GetString() ?? "",

            _ => throw new NotSupportedException(
                $"Registry type '{change.Type}' is not supported."
            )
        };
    }

    private static object ConvertBackupValue(
        JsonElement value,
        RegistryValueKind kind)
    {
        return kind switch
        {
            RegistryValueKind.DWord =>
                value.GetInt32(),

            RegistryValueKind.QWord =>
                value.GetInt64(),

            RegistryValueKind.String =>
                value.GetString() ?? "",

            RegistryValueKind.ExpandString =>
                value.GetString() ?? "",

            _ => throw new NotSupportedException(
                $"Restoring registry type '{kind}' is not supported yet."
            )
        };
    }
}