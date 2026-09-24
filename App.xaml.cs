using System;
using System.Linq;
using System.Windows;
using WinOptimizer.Models;
using WinOptimizer.Services;
using System.Text;
using System.Text.Json;

namespace WinOptimizer;

public partial class App : Application
{
protected override void OnStartup(
    StartupEventArgs e)
{
    base.OnStartup(e);

    bool openPrivacy = false;
    bool openPerformance = false;

    // -----------------------------------------
    // Privacy tweak
    // -----------------------------------------

    string? tweakId =
        GetArgumentValue(
            e.Args,
            "--apply-tweak"
        );

    if (!string.IsNullOrWhiteSpace(
        tweakId))
    {
        try
        {
            TweakEngine tweakEngine =
                new TweakEngine();

            TweakDefinition? tweak =
                tweakEngine.FindTweakById(
                    tweakId
                );

            if (tweak != null &&
                tweakEngine.GetState(tweak) !=
                TweakState.Applied)
            {
                tweakEngine.ApplyTweak(
                    tweak
                );
            }

            openPrivacy = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "Tweak error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );

            openPrivacy = true;
        }
    }


    // -----------------------------------------
    // Startup action
    // -----------------------------------------

    string? startupAction =
        GetArgumentValue(
            e.Args,
            "--startup-action"
        );

    string? startupPayload =
        GetArgumentValue(
            e.Args,
            "--startup-item"
        );

    if (!string.IsNullOrWhiteSpace(
            startupAction) &&
        !string.IsNullOrWhiteSpace(
            startupPayload))
    {
        try
        {
            byte[] bytes =
                Convert.FromBase64String(
                    startupPayload
                );

            string json =
                Encoding.UTF8.GetString(
                    bytes
                );

            StartupItem? item =
                JsonSerializer.Deserialize<
                    StartupItem
                >(json);

            if (item == null)
            {
                throw new InvalidOperationException(
                    "The startup item could not be restored from the command line."
                );
            }

            StartupManager startupManager =
                new StartupManager();

            switch (
                startupAction.ToLowerInvariant())
            {
                case "disable":

                    startupManager.Disable(
                        item
                    );

                    break;

                case "enable":

                    startupManager.Enable(
                        item
                    );

                    break;

                default:

                    throw new InvalidOperationException(
                        $"Unknown startup action: {startupAction}"
                    );
            }

            openPerformance = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "Startup configuration error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );

            openPerformance = true;
        }
    }


    MainWindow window =
        new MainWindow(
            openPrivacy,
            openPerformance
        );

    window.Show();
}
private static string? GetArgumentValue(
    string[] args,
    string argumentName)
{
    for (int i = 0;
         i < args.Length - 1;
         i++)
    {
        if (string.Equals(
            args[i],
            argumentName,
            StringComparison.OrdinalIgnoreCase))
        {
            return args[i + 1];
        }
    }

    return null;
}

    private static string? GetTweakIdFromArguments(
        string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] != "--apply-tweak")
            {
                continue;
            }

            if (i + 1 >= args.Length)
            {
                return null;
            }

            return args[i + 1];
        }

        return null;
    }

    private static bool ApplyStartupTweak(
        string tweakId)
    {
        try
        {
            TweakEngine engine =
                new TweakEngine();

            TweakDefinition? tweak =
                engine.FindTweakById(
                    tweakId
                );

            if (tweak == null)
            {
                MessageBox.Show(
                    $"Tweak '{tweakId}' was not found.",
                    "Tweak not found",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );

                return false;
            }

            if (engine.GetState(tweak) !=
                TweakState.Applied)
            {
                engine.ApplyTweak(tweak);
            }

            return tweak.Category ==
                   "Privacy";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "Startup tweak error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );

            return false;
        }
    }
}