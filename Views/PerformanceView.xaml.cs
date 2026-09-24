using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WinOptimizer.Models;
using WinOptimizer.Services;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace WinOptimizer.Views;

public partial class PerformanceView : UserControl
{
    private readonly StartupAnalyzer _startupAnalyzer;
    private readonly StartupManager _startupManager;
    private readonly StartupHistoryManager _historyManager;

    public PerformanceView()
    {
        InitializeComponent();

        _startupAnalyzer =
            new StartupAnalyzer();

        _startupManager =
            new StartupManager();

        _historyManager =
            new StartupHistoryManager();

        Loaded +=
            PerformanceView_Loaded;
    }

    private async void PerformanceView_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        await LoadStartupApplicationsAsync();
    }

    private async Task
      LoadStartupApplicationsAsync()
    {
        try
        {
            StartupAppsPanel.Children.Clear();

            StartupCountText.Text =
                "Scanning startup applications...";

            var items =
                (await _startupAnalyzer
                    .GetStartupItemsAsync())
                .OrderBy(
                    item => item.Name
                )
                .ToList();

            StartupCountText.Text =
                $"{items.Count} startup entries detected";

            if (items.Count == 0)
            {
                StartupAppsPanel.Children.Add(
                    new TextBlock
                    {
                        Text =
                            "No startup entries detected.",

                        Foreground =
                            Brushes.Gray
                    }
                );

                return;
            }

            foreach (
                StartupItem item in items)
            {
                StartupAppsPanel.Children.Add(
                    CreateStartupCard(item)
                );
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "Startup analysis error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }

 private Border CreateStartupCard(
    StartupItem item)
{
    Border card =
        new Border
        {
            Background =
                new SolidColorBrush(
                    Color.FromRgb(31, 41, 55)
                ),

            CornerRadius =
                new CornerRadius(12),

            Padding =
                new Thickness(20),

            Margin =
                new Thickness(
                    0,
                    0,
                    0,
                    12
                )
        };

    StackPanel content =
        new StackPanel();

    // Application name
    content.Children.Add(
        new TextBlock
        {
            Text = item.Name,

            Foreground =
                Brushes.White,

            FontSize = 17,

            FontWeight =
                FontWeights.SemiBold
        }
    );

    // Source Type
    content.Children.Add(
        new TextBlock
        {
            Text =
                $"Type: {GetSourceTypeText(item.SourceType)}",

            Foreground =
                new SolidColorBrush(
                    Color.FromRgb(
                        148,
                        163,
                        184
                    )
                ),

            FontSize = 12,

            Margin =
                new Thickness(
                    0,
                    6,
                    0,
                    0
                )
        }
    );

    // State Source
    content.Children.Add(
        new TextBlock
        {
            Text =
                $"State Source: {item.StateSource}",

            Foreground =
                new SolidColorBrush(
                    Color.FromRgb(
                        100,
                        116,
                        139
                    )
                ),

            FontSize = 11,

            TextWrapping =
                TextWrapping.Wrap,

            Margin =
                new Thickness(
                    0,
                    3,
                    0,
                    0
                )
        }
    );

    // Status
    content.Children.Add(
        new TextBlock
        {
            Text =
                $"Status: {GetStartupStateText(item.State)}",

            Foreground =
                GetStartupStateColor(item.State),

            FontSize = 12,

            Margin =
                new Thickness(
                    0,
                    4,
                    0,
                    0
                )
        }
    );

    // Source
    content.Children.Add(
        new TextBlock
        {
            Text =
                $"Source: {item.Source}",

            Foreground =
                new SolidColorBrush(
                    Color.FromRgb(
                        148,
                        163,
                        184
                    )
                ),

            FontSize = 12,

            TextWrapping =
                TextWrapping.Wrap,

            Margin =
                new Thickness(
                    0,
                    3,
                    0,
                    0
                )
        }
    );

    // Command
    content.Children.Add(
        new TextBlock
        {
            Text =
                $"Command: {item.Command}",

            Foreground =
                new SolidColorBrush(
                    Color.FromRgb(
                        203,
                        213,
                        225
                    )
                ),

            FontSize = 12,

            TextWrapping =
                TextWrapping.Wrap,

            Margin =
                new Thickness(
                    0,
                    8,
                    0,
                    0
                )
        }
    );

    // Admin hint
    if (item.RequiresAdmin)
    {
        content.Children.Add(
            new TextBlock
            {
                Text =
                    "Administrator required to modify",

                Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(
                            245,
                            158,
                            11
                        )
                    ),

                FontSize = 11,

                Margin =
                    new Thickness(
                        0,
                        8,
                        0,
                        0
                    )
            }
        );
    }

    // Enable / Disable button
   bool canModify =
    item.SourceType ==
        StartupSourceType.Registry ||
    item.SourceType ==
        StartupSourceType.StartupFolder;

if (canModify &&
    item.State != StartupState.Unknown)
    {
        Button actionButton =
            new Button
            {
                Content =
                    item.State ==
                    StartupState.Enabled
                        ? "Disable"
                        : "Enable",

                Padding =
                    new Thickness(
                        15,
                        8,
                        15,
                        8
                    ),

                Margin =
                    new Thickness(
                        0,
                        12,
                        0,
                        0
                    ),

                HorizontalAlignment =
                    HorizontalAlignment.Left,

                Tag = item
            };

        actionButton.Click +=
            StartupActionButton_Click;

        content.Children.Add(
            actionButton
        );
    }
   else if (
    item.SourceType ==
    StartupSourceType.PackagedStartupTask)
{
    Button manageButton =
        new Button
        {
            Content =
                "Manage in Windows",

            Padding =
                new Thickness(
                    15,
                    8,
                    15,
                    8
                ),

            Margin =
                new Thickness(
                    0,
                    12,
                    0,
                    0
                ),

            HorizontalAlignment =
                HorizontalAlignment.Left,

            Tag = item
        };

    manageButton.Click +=
        PackagedStartupManageButton_Click;

    content.Children.Add(
        manageButton
    );

    content.Children.Add(
        new TextBlock
        {
            Text =
                "Packaged startup apps are managed through Windows Settings.",

            Foreground =
                new SolidColorBrush(
                    Color.FromRgb(
                        100,
                        116,
                        139
                    )
                ),

            FontSize = 11,

            TextWrapping =
                TextWrapping.Wrap,

            Margin =
                new Thickness(
                    0,
                    6,
                    0,
                    0
                )
        }
    );
}
else
{
    content.Children.Add(
        new TextBlock
        {
            Text =
                "Modification unavailable",

            Foreground =
                new SolidColorBrush(
                    Color.FromRgb(
                        100,
                        116,
                        139
                    )
                ),

            FontSize = 11,

            Margin =
                new Thickness(
                    0,
                    10,
                    0,
                    0
                )
        }
    );
}

    card.Child =
        content;

    return card;
}
private void PackagedStartupManageButton_Click(
    object sender,
    RoutedEventArgs e)
{
    try
    {
        Process.Start(
            new ProcessStartInfo
            {
                FileName =
                    "ms-settings:startupapps",

                UseShellExecute =
                    true
            }
        );
    }
    catch (Exception ex)
    {
        MessageBox.Show(
            ex.ToString(),
            "Unable to open Startup Settings",
            MessageBoxButton.OK,
            MessageBoxImage.Error
        );
    }
}
    private static string GetSourceTypeText(
    StartupSourceType sourceType)
    {
        return sourceType switch
        {
            StartupSourceType.Registry =>
                "Registry",

            StartupSourceType.StartupFolder =>
                "Startup Folder",

            StartupSourceType.PackagedStartupTask =>
                "Packaged App",

            _ =>
                "Unknown"
        };
    }
    private static string GetStartupStateText(
    StartupState state)
    {
        return state switch
        {
            StartupState.Enabled =>
                "Enabled",

            StartupState.Disabled =>
                "Disabled",

            _ =>
                "Unknown"
        };
    }

    private static Brush GetStartupStateColor(
        StartupState state)
    {
        return state switch
        {
            StartupState.Enabled =>
                new SolidColorBrush(
                    Color.FromRgb(
                        34,
                        197,
                        94
                    )
                ),

            StartupState.Disabled =>
                new SolidColorBrush(
                    Color.FromRgb(
                        148,
                        163,
                        184
                    )
                ),

            _ =>
                new SolidColorBrush(
                    Color.FromRgb(
                        245,
                        158,
                        11
                    )
                )
        };
    }
   private async void StartupActionButton_Click(
    object sender,
    RoutedEventArgs e)
{
    if (sender is not Button button ||
        button.Tag is not StartupItem item)
    {
        return;
    }

    string action =
        item.State == StartupState.Enabled
            ? "disable"
            : "enable";

    MessageBoxResult result =
        MessageBox.Show(
            $"{action} '{item.Name}' at startup?",
            "Startup configuration",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question
        );

    if (result != MessageBoxResult.Yes)
    {
        return;
    }

    // Entry needs administrator privileges.
    if (item.RequiresAdmin &&
        !AdminService.IsRunningAsAdministrator())
    {
        string json =
            JsonSerializer.Serialize(item);

        string payload =
            Convert.ToBase64String(
                Encoding.UTF8.GetBytes(json)
            );

        string arguments =
            $"--startup-action {action} " +
            $"--startup-item {payload}";

        bool started =
            AdminService.TryStartElevatedInstance(
                arguments
            );

        if (started)
        {
            Application.Current.Shutdown();
        }

        return;
    }

    try
    {
        if (item.State == StartupState.Enabled)
        {
            _startupManager.Disable(item);
        }
        else
        {
            _startupManager.Enable(item);
        }

        await LoadStartupApplicationsAsync();
    }
    catch (Exception ex)
    {
        MessageBox.Show(
            ex.ToString(),
            "Startup configuration error",
            MessageBoxButton.OK,
            MessageBoxImage.Error
        );
    }
}
}
