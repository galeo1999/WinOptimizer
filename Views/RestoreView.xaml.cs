using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WinOptimizer.Models;
using WinOptimizer.Services;

namespace WinOptimizer.Views;

public partial class RestoreView : UserControl
{
    private readonly TweakEngine _tweakEngine;

    private readonly StartupManager _startupManager;

    public RestoreView()
    {
        InitializeComponent();

        _tweakEngine =
            new TweakEngine();

        _startupManager =
            new StartupManager();

        Loaded +=
            RestoreView_Loaded;
    }

    private void RestoreView_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        LoadHistory();
    }

    private void LoadHistory()
    {
        LoadPrivacyHistory();

        LoadStartupHistory();
    }


    // =========================================================
    // Privacy history
    // =========================================================

    private void LoadPrivacyHistory()
    {
        PrivacyHistoryPanel
            .Children
            .Clear();

        var history =
            _tweakEngine
                .GetHistory()
                .OrderByDescending(
                    entry =>
                        entry.CreatedAt
                )
                .ToList();

        if (history.Count == 0)
        {
            PrivacyHistoryPanel.Children.Add(
                CreateEmptyText(
                    "No privacy restore points available."
                )
            );

            return;
        }

        foreach (
            TweakHistoryEntry entry
            in history)
        {
            PrivacyHistoryPanel
                .Children
                .Add(
                    CreatePrivacyHistoryCard(
                        entry
                    )
                );
        }
    }

    private Border CreatePrivacyHistoryCard(
        TweakHistoryEntry entry)
    {
        Border card =
            CreateCard();

        Grid grid =
            CreateCardGrid();

        StackPanel information =
            new StackPanel();

        // Name
        information.Children.Add(
            new TextBlock
            {
                Text =
                    entry.TweakName,

                Foreground =
                    Brushes.White,

                FontSize = 17,

                FontWeight =
                    FontWeights.SemiBold
            }
        );

        // Type
        information.Children.Add(
            CreateSecondaryText(
                "Type: Privacy / Registry",
                5
            )
        );

        // Date
        information.Children.Add(
            CreateSecondaryText(
                entry.CreatedAt
                    .ToLocalTime()
                    .ToString(
                        "dd.MM.yyyy HH:mm:ss"
                    ),
                5
            )
        );

        // Status
        TextBlock statusText =
            new TextBlock
            {
                Text =
                    $"Status: {entry.Status}",

                FontSize = 13,

                Foreground =
                    GetStatusColor(
                        entry.Status
                    ),

                Margin =
                    new Thickness(
                        0,
                        10,
                        0,
                        0
                    )
            };

        information.Children.Add(
            statusText
        );

        // Registry backup information
        foreach (
            RegistryBackup backup
            in entry.RegistryBackups)
        {
            string previousValue =
                backup.Existed
                    ? backup.Value?.ToString()
                      ?? "Unknown"
                    : "Not present";

            information.Children.Add(
                new TextBlock
                {
                    Text =
                        $"{backup.Name}: previous value = {previousValue}",

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
                            5,
                            0,
                            0
                        )
                }
            );
        }

        Grid.SetColumn(
            information,
            0
        );

        grid.Children.Add(
            information
        );


        // Restore button
        bool canRestore =
            _tweakEngine
                .CanRestore(
                    entry.Id
                );

        Button restoreButton =
            CreateRestoreButton(
                GetPrivacyRestoreButtonText(
                    entry,
                    canRestore
                ),
                canRestore,
                entry.Id
            );

        restoreButton.Click +=
            PrivacyRestoreButton_Click;

        Grid.SetColumn(
            restoreButton,
            1
        );

        grid.Children.Add(
            restoreButton
        );

        card.Child = grid;

        return card;
    }


    // =========================================================
    // Startup history
    // =========================================================

    private void LoadStartupHistory()
    {
        StartupHistoryPanel
            .Children
            .Clear();

        var history =
            _startupManager
                .GetHistory()
                .OrderByDescending(
                    entry =>
                        entry.CreatedAt
                )
                .ToList();

        if (history.Count == 0)
        {
            StartupHistoryPanel.Children.Add(
                CreateEmptyText(
                    "No startup restore points available."
                )
            );

            return;
        }

        foreach (
            StartupHistoryEntry entry
            in history)
        {
            StartupHistoryPanel
                .Children
                .Add(
                    CreateStartupHistoryCard(
                        entry
                    )
                );
        }
    }

    private Border CreateStartupHistoryCard(
        StartupHistoryEntry entry)
    {
        Border card =
            CreateCard();

        Grid grid =
            CreateCardGrid();

        StackPanel information =
            new StackPanel();


        // Application name
        information.Children.Add(
            new TextBlock
            {
                Text =
                    entry.StartupName,

                Foreground =
                    Brushes.White,

                FontSize = 17,

                FontWeight =
                    FontWeights.SemiBold
            }
        );


        // Type
        information.Children.Add(
            CreateSecondaryText(
                "Type: Startup Application",
                5
            )
        );


        // Action
        information.Children.Add(
            new TextBlock
            {
                Text =
                    $"Action: {entry.Action}",

                Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(
                            203,
                            213,
                            225
                        )
                    ),

                FontSize = 13,

                Margin =
                    new Thickness(
                        0,
                        8,
                        0,
                        0
                    )
            }
        );


        // Date
        information.Children.Add(
            CreateSecondaryText(
                entry.CreatedAt
                    .ToLocalTime()
                    .ToString(
                        "dd.MM.yyyy HH:mm:ss"
                    ),
                5
            )
        );


        // Status
        information.Children.Add(
            new TextBlock
            {
                Text =
                    $"Status: {entry.Status}",

                Foreground =
                    GetStatusColor(
                        entry.Status
                    ),

                FontSize = 13,

                Margin =
                    new Thickness(
                        0,
                        10,
                        0,
                        0
                    )
            }
        );


        // Backup info
        string backupText =
            entry.Backup.Existed
                ? "Previous StartupApproved value saved."
                : "StartupApproved value did not previously exist.";

        information.Children.Add(
            new TextBlock
            {
                Text = backupText,

                Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(
                            203,
                            213,
                            225
                        )
                    ),

                FontSize = 12,

                Margin =
                    new Thickness(
                        0,
                        5,
                        0,
                        0
                    )
            }
        );


        // Registry location
        information.Children.Add(
            new TextBlock
            {
                Text =
                    $"{entry.Backup.Hive}\\{entry.Backup.Path}\\{entry.Backup.Name}",

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
                        5,
                        0,
                        0
                    )
            }
        );


        Grid.SetColumn(
            information,
            0
        );

        grid.Children.Add(
            information
        );


        // Restore button
        bool canRestore =
            _startupManager
                .CanRestore(
                    entry.Id
                );

        Button restoreButton =
            CreateRestoreButton(
                GetStartupRestoreButtonText(
                    entry,
                    canRestore
                ),
                canRestore,
                entry.Id
            );

        restoreButton.Click +=
            StartupRestoreButton_Click;

        Grid.SetColumn(
            restoreButton,
            1
        );

        grid.Children.Add(
            restoreButton
        );


        card.Child = grid;

        return card;
    }


    // =========================================================
    // Privacy restore action
    // =========================================================

    private void PrivacyRestoreButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not string historyId)
        {
            return;
        }

        MessageBoxResult result =
            MessageBox.Show(
                "Restore the previous Windows setting?",
                "Restore privacy change",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

        if (result !=
            MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            _tweakEngine
                .RestoreTweak(
                    historyId
                );

            LoadHistory();

            MessageBox.Show(
                "The previous setting was restored successfully.",
                "Restore complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "Restore error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }


    // =========================================================
    // Startup restore action
    // =========================================================

    private void StartupRestoreButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not string historyId)
        {
            return;
        }

        MessageBoxResult result =
            MessageBox.Show(
                "Restore the previous startup state?",
                "Restore startup change",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

        if (result !=
            MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            _startupManager
                .Restore(
                    historyId
                );

            LoadHistory();

            MessageBox.Show(
                "The previous startup state was restored successfully.",
                "Restore complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "Startup restore error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }


    // =========================================================
    // Shared UI helpers
    // =========================================================

    private static Border CreateCard()
    {
        return new Border
        {
            Background =
                new SolidColorBrush(
                    Color.FromRgb(
                        31,
                        41,
                        55
                    )
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
                    15
                )
        };
    }

    private static Grid CreateCardGrid()
    {
        Grid grid =
            new Grid();

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star
                    )
            }
        );

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    GridLength.Auto
            }
        );

        return grid;
    }

    private static TextBlock CreateSecondaryText(
        string text,
        double topMargin)
    {
        return new TextBlock
        {
            Text = text,

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
                    topMargin,
                    0,
                    0
                )
        };
    }

    private static TextBlock CreateEmptyText(
        string text)
    {
        return new TextBlock
        {
            Text = text,

            Foreground =
                new SolidColorBrush(
                    Color.FromRgb(
                        148,
                        163,
                        184
                    )
                ),

            FontSize = 14,

            Margin =
                new Thickness(
                    0,
                    5,
                    0,
                    10
                )
        };
    }

    private static Button CreateRestoreButton(
        string content,
        bool enabled,
        string historyId)
    {
        return new Button
        {
            Content =
                content,

            Padding =
                new Thickness(
                    15,
                    8,
                    15,
                    8
                ),

            Margin =
                new Thickness(
                    20,
                    0,
                    0,
                    0
                ),

            VerticalAlignment =
                VerticalAlignment.Center,

            IsEnabled =
                enabled,

            Tag =
                historyId
        };
    }


    // =========================================================
    // Button text
    // =========================================================

    private static string GetPrivacyRestoreButtonText(
        TweakHistoryEntry entry,
        bool canRestore)
    {
        if (canRestore)
        {
            return "Restore";
        }

        return entry.Status switch
        {
            "Restored" =>
                "Restored",

            "RolledBack" =>
                "Rolled back",

            "Applied" =>
                "Superseded",

            _ =>
                entry.Status
        };
    }

    private static string GetStartupRestoreButtonText(
        StartupHistoryEntry entry,
        bool canRestore)
    {
        if (canRestore)
        {
            return "Restore";
        }

        return entry.Status switch
        {
            "Restored" =>
                "Restored",

            "RolledBack" =>
                "Rolled back",

            "Applied" =>
                "Superseded",

            _ =>
                entry.Status
        };
    }


    // =========================================================
    // Status colors
    // =========================================================

    private static Brush GetStatusColor(
        string status)
    {
        return status switch
        {
            "Applied" =>
                new SolidColorBrush(
                    Color.FromRgb(
                        34,
                        197,
                        94
                    )
                ),

            "Restored" =>
                new SolidColorBrush(
                    Color.FromRgb(
                        96,
                        165,
                        250
                    )
                ),

            "RolledBack" =>
                new SolidColorBrush(
                    Color.FromRgb(
                        245,
                        158,
                        11
                    )
                ),

            _ =>
                new SolidColorBrush(
                    Color.FromRgb(
                        148,
                        163,
                        184
                    )
                )
        };
    }
}