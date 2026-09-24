using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;
using WinOptimizer.Models;
using System.Threading.Tasks;

namespace WinOptimizer.Services;

public class StartupAnalyzer
{
    private readonly PackagedStartupAnalyzer
        _packagedStartupAnalyzer =
            new PackagedStartupAnalyzer();

    public async Task<List<StartupItem>>
        GetStartupItemsAsync()
    {
        List<StartupItem> items = [];

        ReadRegistryStartupItems(items);

        ReadStartupFolder(
            items,
            Environment.GetFolderPath(
                Environment.SpecialFolder.Startup
            ),
            "Current User"
        );

        ReadStartupFolder(
            items,
            Environment.GetFolderPath(
                Environment.SpecialFolder.CommonStartup
            ),
            "All Users"
        );

        List<StartupItem> packagedItems =
            await _packagedStartupAnalyzer
                .GetStartupItemsAsync();

        items.AddRange(
            packagedItems
        );

        return items;
    }

    private static void ReadRegistryStartupItems(
        List<StartupItem> items)
    {
        ReadRegistryKey(
     items,
     Registry.CurrentUser,
     @"Software\Microsoft\Windows\CurrentVersion\Run",
     @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run",
     "Current User",
     requiresAdmin: false
 );

        ReadRegistryKey(
            items,
            Registry.LocalMachine,
            @"Software\Microsoft\Windows\CurrentVersion\Run",
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run",
            "All Users",
            requiresAdmin: true
        );

        ReadRegistryKey(
            items,
            Registry.LocalMachine,
            @"Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Run",
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32",
            "All Users (32-bit)",
            requiresAdmin: true
        );
    }

    private static void ReadRegistryKey(
     List<StartupItem> items,
     RegistryKey hive,
     string path,
     string startupApprovedPath,
     string scope,
     bool requiresAdmin)
    {
        using RegistryKey? key =
            hive.OpenSubKey(path);

        if (key == null)
        {
            return;
        }

        foreach (string valueName in key.GetValueNames())
        {
            object? value =
                key.GetValue(
                    valueName,
                    null,
                    RegistryValueOptions.DoNotExpandEnvironmentNames
                );

            if (value == null)
            {
                continue;
            }

            StartupState state =
                GetStartupState(
                    hive,
                    startupApprovedPath,
                    valueName
                );

            items.Add(
    new StartupItem
    {
        Name = valueName,

        Command =
            value.ToString() ?? "",

        Source =
            $@"Registry\{path}",

        SourceType =
            StartupSourceType.Registry,

        Scope = scope,

        RequiresAdmin =
            requiresAdmin,

        State = state,

        StateSource =
            $"{hive.Name}\\{startupApprovedPath}",

        StartupApprovedHive =
            hive == Registry.CurrentUser
                ? "CurrentUser"
                : "LocalMachine",

        StartupApprovedPath =
            startupApprovedPath,

        StartupApprovedValueName =
            valueName
    }
);
        }
    }
    private static StartupState GetStartupState(
    RegistryKey hive,
    string startupApprovedPath,
    string valueName)
    {
        using RegistryKey? key =
            hive.OpenSubKey(startupApprovedPath);

        /*
         * Der eigentliche Startup-Eintrag existiert bereits,
         * sonst wären wir gar nicht hier.
         *
         * Fehlt StartupApproved vollständig, gibt es
         * keine explizite Windows-Sperre.
         */
        if (key == null)
        {
            return StartupState.Enabled;
        }

        object? value =
            key.GetValue(valueName);

        /*
         * Kein StartupApproved-Eintrag für diese App:
         * Startup-Eintrag ist vorhanden und wurde
         * nicht explizit deaktiviert.
         */
        if (value == null)
        {
            return StartupState.Enabled;
        }

        if (value is not byte[] data ||
            data.Length == 0)
        {
            return StartupState.Unknown;
        }

        byte stateByte =
            data[0];

        return stateByte switch
        {
            0x02 => StartupState.Enabled,
            0x06 => StartupState.Enabled,

            // Observed Windows StartupApproved disabled states.
            // This binary format is not a documented public API.
            0x01 => StartupState.Disabled,
            0x03 => StartupState.Disabled,
            0x07 => StartupState.Disabled,

            _ => StartupState.Unknown
        };
    }

    private static void ReadStartupFolder(
     List<StartupItem> items,
     string folderPath,
     string scope)
    {
        if (string.IsNullOrWhiteSpace(folderPath) ||
            !Directory.Exists(folderPath))
        {
            return;
        }

        foreach (string file in Directory.GetFiles(folderPath))
        {
            string fileName =
                Path.GetFileName(file);

            // Windows folder metadata
            if (fileName.Equals(
                "desktop.ini",
                StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            FileAttributes attributes =
                File.GetAttributes(file);

            if (attributes.HasFlag(FileAttributes.System))
            {
                continue;
            }

            RegistryKey startupApprovedHive =
                scope == "All Users"
                    ? Registry.LocalMachine
                    : Registry.CurrentUser;

            StartupState state =
                GetStartupState(
                    startupApprovedHive,
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder",
                    fileName
                );

      items.Add(
    new StartupItem
    {
        Name =
            Path.GetFileNameWithoutExtension(file),

        Command = file,

        Source = "Startup Folder",

        SourceType =
            StartupSourceType.StartupFolder,

        Scope = scope,

        RequiresAdmin =
            scope == "All Users",

        State = state,

        StateSource =
            $"{startupApprovedHive.Name}\\" +
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder",

        StartupApprovedHive =
            startupApprovedHive == Registry.CurrentUser
                ? "CurrentUser"
                : "LocalMachine",

        StartupApprovedPath =
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder",

        StartupApprovedValueName =
            Path.GetFileName(file)
    }
);
        }
    }
}