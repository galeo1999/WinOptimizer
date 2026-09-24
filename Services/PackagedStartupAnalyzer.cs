using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Win32;
using Windows.Management.Deployment;
using Windows.Storage;
using WinOptimizer.Models;

namespace WinOptimizer.Services;

public class PackagedStartupAnalyzer
{
    public async Task<List<StartupItem>>
        GetStartupItemsAsync()
    {
        List<StartupItem> items = [];

        PackageManager packageManager =
            new PackageManager();

        var packages =
            packageManager.FindPackagesForUser(
                string.Empty
            );

        foreach (var package in packages)
        {
            try
            {
                StorageFile manifestFile =
                    await package.InstalledLocation
                        .GetFileAsync(
                            "AppxManifest.xml"
                        );

                string manifestText =
                    await FileIO.ReadTextAsync(
                        manifestFile
                    );

                XDocument document =
                    XDocument.Parse(
                        manifestText
                    );

                var startupExtensions =
                    document
                        .Descendants()
                        .Where(element =>
                            element.Name.LocalName ==
                            "Extension" &&

                            string.Equals(
                                element
                                    .Attribute("Category")
                                    ?.Value,

                                "windows.startupTask",

                                StringComparison
                                    .OrdinalIgnoreCase
                            )
                        );

                foreach (
                    XElement extension
                    in startupExtensions)
                {
                    XElement? startupTask =
                        extension
                            .Descendants()
                            .FirstOrDefault(
                                element =>
                                    element.Name.LocalName ==
                                    "StartupTask"
                            );

                    if (startupTask == null)
                    {
                        continue;
                    }

                    string? taskId =
                        startupTask
                            .Attribute("TaskId")
                            ?.Value;

                    if (string.IsNullOrWhiteSpace(
                        taskId))
                    {
                        continue;
                    }

                    string packageFamilyName =
                        package.Id.FamilyName;

                    string statePath =
                        @"Software\Classes\Local Settings\" +
                        @"Software\Microsoft\Windows\" +
                        @"CurrentVersion\AppModel\SystemAppData\" +
                        $"{packageFamilyName}\\{taskId}";

                    bool registered =
                        TryGetStartupState(
                            statePath,
                            out StartupState state
                        );

                    /*
                     * Laut Windows-Dokumentation muss eine App
                     * grundsätzlich mindestens einmal gestartet
                     * worden sein, damit ihr StartupTask
                     * registriert wird.
                     *
                     * Wenn wir keinen registrierten Zustand finden,
                     * zeigen wir den Manifest-Eintrag deshalb
                     * zunächst nicht an.
                     */
                    if (!registered)
                    {
                        continue;
                    }

                    string displayName =
                        GetDisplayName(
                            package.DisplayName,
                            startupTask,
                            taskId
                        );

                    string executable =
                        extension
                            .Attribute("Executable")
                            ?.Value
                        ?? "(packaged startup task)";

                    items.Add(
                        new StartupItem
                        {
                            Name = displayName,

                            Command = executable,

                            Source =
                                "Packaged Startup Task",

                            SourceType =
                                StartupSourceType
                                    .PackagedStartupTask,

                            Scope =
                                "Current User",

                            RequiresAdmin =
                                false,

                            State = state,

                            StateSource =
                                $"HKEY_CURRENT_USER\\{statePath}"
                        }
                    );
                }
            }
            catch
            {
                /*
                 * Ein einzelnes Paket darf nicht
                 * den kompletten Startup-Scan
                 * abbrechen.
                 */
            }
        }

        return items;
    }

    private static bool TryGetStartupState(
        string registryPath,
        out StartupState state)
    {
        using RegistryKey? key =
            Registry.CurrentUser.OpenSubKey(
                registryPath
            );

        if (key == null)
        {
            state = StartupState.Unknown;
            return false;
        }

        object? value =
            key.GetValue("State");

        if (value == null)
        {
            state = StartupState.Unknown;
            return true;
        }

        int stateValue;

        try
        {
            stateValue =
                Convert.ToInt32(value);
        }
        catch
        {
            state = StartupState.Unknown;
            return true;
        }

        state = stateValue switch
        {
            // Disabled
            0 => StartupState.Disabled,

            // DisabledByUser
            1 => StartupState.Disabled,

            // Enabled
            2 => StartupState.Enabled,

            // DisabledByPolicy
            3 => StartupState.Disabled,

            // EnabledByPolicy
            4 => StartupState.Enabled,

            _ => StartupState.Unknown
        };

        return true;
    }

    private static string GetDisplayName(
        string packageDisplayName,
        XElement startupTask,
        string taskId)
    {
        string? manifestDisplayName =
            startupTask
                .Attribute("DisplayName")
                ?.Value;

        if (!string.IsNullOrWhiteSpace(
                manifestDisplayName) &&
            !manifestDisplayName.StartsWith(
                "ms-resource:",
                StringComparison.OrdinalIgnoreCase))
        {
            return manifestDisplayName;
        }

        if (!string.IsNullOrWhiteSpace(
                packageDisplayName) &&
            !packageDisplayName.StartsWith(
                "ms-resource:",
                StringComparison.OrdinalIgnoreCase))
        {
            return packageDisplayName;
        }

        return taskId;
    }
}