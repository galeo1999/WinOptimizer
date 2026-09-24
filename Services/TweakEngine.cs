using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Win32;
using WinOptimizer.Models;

namespace WinOptimizer.Services;

public class TweakEngine
{
    private readonly RegistryService _registryService;
    private readonly RestoreManager _restoreManager;
    public TweakEngine()
    {
        _registryService =
            new RegistryService();

        _restoreManager =
            new RestoreManager();
    }
    public List<TweakHistoryEntry> GetHistory()
    {
        return _restoreManager.LoadHistory();
    }
    public List<TweakDefinition> LoadTweaks(string fileName)
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "Config",
            fileName
        );

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Tweak configuration not found: {path}"
            );
        }

        string json = File.ReadAllText(path);

        return JsonSerializer.Deserialize<List<TweakDefinition>>(
                   json,
                   new JsonSerializerOptions
                   {
                       PropertyNameCaseInsensitive = true
                   }
               )
               ?? [];
    }

    public TweakState GetState(TweakDefinition tweak)
    {

        if (tweak.RegistryChanges.Count == 0)
        {
            return TweakState.Unknown;
        }

        int matchingChanges = 0;

        foreach (RegistryChange change in tweak.RegistryChanges)
        {
            if (_registryService.ValueMatches(change))
            {
                matchingChanges++;
            }
        }

        if (matchingChanges == tweak.RegistryChanges.Count)
        {
            return TweakState.Applied;
        }

        if (matchingChanges == 0)
        {
            return TweakState.NotApplied;
        }

        return TweakState.Partial;
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
    public void RestoreTweak(string historyId)
    {
        TweakHistoryEntry history =
            _restoreManager.GetById(
                historyId
            )
            ?? throw new InvalidOperationException(
                $"History entry '{historyId}' was not found."
            );

        foreach (
            RegistryBackup backup
            in history.RegistryBackups
                .AsEnumerable()
                .Reverse())
        {
            _registryService.Restore(
                backup
            );
        }

        history.Status =
            "Restored";

        history.RestoredAt =
            DateTimeOffset.Now;

        _restoreManager.Update(history);
    }
    public string ApplyTweak(
    TweakDefinition tweak)
    {
        if (tweak.RequiresAdmin &&
    !AdminService.IsRunningAsAdministrator())
        {
            throw new UnauthorizedAccessException(
                $"Tweak '{tweak.Name}' requires administrator privileges."
            );
        }
        if (GetState(tweak) == TweakState.Applied)
        {
            throw new InvalidOperationException(
                $"Tweak '{tweak.Name}' is already applied."
            );
        }
        TweakHistoryEntry history =
            new TweakHistoryEntry
            {
                TweakId = tweak.Id,
                TweakName = tweak.Name
            };

        foreach (RegistryChange change
                 in tweak.RegistryChanges)
        {
            history.RegistryBackups.Add(
                _registryService.CreateBackup(
                    change
                )
            );
        }

        // Backup zuerst speichern.
        _restoreManager.Add(history);

        try
        {
            foreach (RegistryChange change
                     in tweak.RegistryChanges)
            {
                _registryService.Apply(change);
            }

            if (GetState(tweak) !=
                TweakState.Applied)
            {
                throw new InvalidOperationException(
                    $"Tweak '{tweak.Name}' could not be verified."
                );
            }

            history.Status = "Applied";

            _restoreManager.Update(history);

            return history.Id;
        }
        catch
        {
            // Falls Apply teilweise fehlschlägt:
            // alten Zustand sofort wiederherstellen.

            foreach (
                RegistryBackup backup
                in history.RegistryBackups.AsEnumerable().Reverse())
            {
                _registryService.Restore(
                    backup
                );
            }

            history.Status =
                "RolledBack";

            _restoreManager.Update(history);

            throw;
        }
    }
    public TweakDefinition? FindTweakById(
    string tweakId)
{
    string[] configurationFiles =
    {
        "privacy.json"
    };

    foreach (string file in configurationFiles)
    {
        List<TweakDefinition> tweaks =
            LoadTweaks(file);

        TweakDefinition? tweak =
            tweaks.FirstOrDefault(
                item => item.Id == tweakId
            );

        if (tweak != null)
        {
            return tweak;
        }
    }

    return null;
}
    public bool CanRestore(string historyId)
    {
        List<TweakHistoryEntry> history =
            _restoreManager.LoadHistory();

        TweakHistoryEntry? entry =
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

        TweakHistoryEntry? latestEntryForTweak =
            history
                .Where(item =>
                    item.TweakId == entry.TweakId)
                .OrderByDescending(item =>
                    item.CreatedAt)
                .FirstOrDefault();

        return latestEntryForTweak?.Id == entry.Id;
    }
}