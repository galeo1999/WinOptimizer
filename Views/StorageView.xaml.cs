using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WinOptimizer.Models;
using WinOptimizer.Services;

namespace WinOptimizer.Views;

public partial class StorageView : UserControl
{
    private readonly StorageAnalyzer
        _storageAnalyzer;
    private readonly StorageCleaner
    _storageCleaner;

    public StorageView()
    {
        InitializeComponent();

        _storageAnalyzer =
            new StorageAnalyzer();

        _storageCleaner =
            new StorageCleaner();
    }

    private async void AnalyzeStorageButton_Click(
     object sender,
     RoutedEventArgs e)
    {
        await AnalyzeStorageAsync();
    }

    private async Task AnalyzeStorageAsync()
    {
        AnalyzeStorageButton.IsEnabled =
            false;

        StorageStatusText.Text =
            "Scanning...";

        TotalStorageText.Text =
            "Analyzing...";

        StorageSummaryText.Text =
            "Scanning storage locations.";

        CleanupPotentialText.Text =
            "";

        StorageResultsPanel.Children.Clear();

        try
        {
            var locations =
                await _storageAnalyzer
                    .AnalyzeAsync();

            ShowStorageResults(
                locations
            );

            StorageStatusText.Text =
                "Analysis complete";
        }
        catch (Exception ex)
        {
            StorageStatusText.Text =
                "Analysis failed";

            MessageBox.Show(
                ex.ToString(),
                "Storage analysis error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
        finally
        {
            AnalyzeStorageButton.IsEnabled =
                true;
        }
    }

    private void ShowStorageResults(
        System.Collections.Generic.List<
            StorageLocationInfo
        > locations)
    {
        long totalBytes =
            locations.Sum(
                item => item.SizeBytes
            );

        long totalFiles =
            locations.Sum(
                item => item.FileCount
            );

        int skippedItems =
            locations.Sum(
                item => item.SkippedItems
            );
        long cleanupBytes =
locations
    .Where(
        item =>
            item.IsCleanupCandidate
    )
    .Sum(
        item =>
            item.SizeBytes
    );



        CleanupPotentialText.Text =
    $"Potential cleanup: {FormatBytes(cleanupBytes)}";
        TotalStorageText.Text =
            FormatBytes(
                totalBytes
            );

        StorageSummaryText.Text =
            $"{totalFiles:N0} files detected";

        if (skippedItems > 0)
        {
            StorageSummaryText.Text +=
                $" · {skippedItems} items skipped";
        }


        foreach (
            StorageLocationInfo location
            in locations)
        {
            StorageResultsPanel
                .Children
                .Add(
                    CreateStorageCard(
                        location
                    )
                );
        }
    }

    private Border CreateStorageCard(
        StorageLocationInfo location)
    {
        Border card =
            new Border
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

        StackPanel information =
            new StackPanel();

        StackPanel actions =
            new StackPanel
            {
                Margin =
                    new Thickness(
                        20,
                        0,
                        0,
                        0
                    ),

                HorizontalAlignment =
                    HorizontalAlignment.Right
            };

        actions.Children.Add(
            new TextBlock
            {
                Text =
                    FormatBytes(
                        location.SizeBytes
                    ),

                Foreground =
                    Brushes.White,

                FontSize = 20,

                FontWeight =
                    FontWeights.SemiBold,

                HorizontalAlignment =
                    HorizontalAlignment.Right
            }
        );
        if (location.IsCleanupCandidate)
        {
            Button cleanButton =
                new Button
                {
                    Content =
                        location.Type ==
                            StorageLocationType.WindowsTemp &&
                        !AdminService
                            .IsRunningAsAdministrator()
                            ? "Requires Admin"
                            : "Clean",

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
                        HorizontalAlignment.Right,

                    IsEnabled =
                        location.Type !=
                            StorageLocationType.WindowsTemp ||
                        AdminService
                            .IsRunningAsAdministrator(),

                    Tag =
                        location
                };

            cleanButton.Click +=
                CleanStorageButton_Click;

            actions.Children.Add(
                cleanButton
            );
        }
        Grid.SetColumn(
            actions,
            1
        );

        grid.Children.Add(
            actions
        );


        // Name
        information.Children.Add(
            new TextBlock
            {
                Text =
                    location.Name,

                Foreground =
                    Brushes.White,

                FontSize = 17,

                FontWeight =
                    FontWeights.SemiBold
            }
        );


        // Description
        information.Children.Add(
            new TextBlock
            {
                Text =
                    location.Description,

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
                        5,
                        0,
                        0
                    )
            }
        );

        TextBlock cleanupStatusText =
    new TextBlock
    {
        FontSize = 12,

        FontWeight =
            FontWeights.SemiBold,

        Margin =
            new Thickness(
                0,
                6,
                0,
                0
            )
    };

        if (location.IsCleanupCandidate)
        {
            cleanupStatusText.Text =
                "Cleanup candidate";

            cleanupStatusText.Foreground =
                new SolidColorBrush(
                    Color.FromRgb(
                        34,
                        197,
                        94
                    )
                );
        }
        else
        {
            cleanupStatusText.Text =
                "Analysis only";

            cleanupStatusText.Foreground =
                new SolidColorBrush(
                    Color.FromRgb(
                        148,
                        163,
                        184
                    )
                );
        }

        information.Children.Add(
            cleanupStatusText
        );

        if (location.RequiresAdmin)
        {
            information.Children.Add(
                new TextBlock
                {
                    Text =
                        "Administrator privileges required for cleanup",

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
                            5,
                            0,
                            0
                        )
                }
            );
        }


        // Path
        information.Children.Add(
            new TextBlock
            {
                Text =
                    $"Path: {location.Path}",

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


        // Files
        information.Children.Add(
            new TextBlock
            {
                Text =
                    $"Files: {location.FileCount:N0}",

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
                        8,
                        0,
                        0
                    )
            }
        );


        // Skipped items
        if (location.IsPartial)
        {
            information.Children.Add(
                new TextBlock
                {
                    Text =
                        $"Skipped items: {location.SkippedItems}",

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

        card.Child =
            grid;

        return card;
    }
    private async void CleanStorageButton_Click(
    object sender,
    RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not StorageLocationInfo location)
        {
            return;
        }


        string warning =
            location.Type ==
                StorageLocationType.RecycleBin
                ? "The Recycle Bin will be emptied. This cannot be undone by WinOptimizer."
                : "Temporary files older than 24 hours will be deleted. This cannot be undone by WinOptimizer.";


        MessageBoxResult confirmation =
            MessageBox.Show(
                $"{warning}\n\n" +
                $"Location: {location.Name}\n" +
                $"Detected size: {FormatBytes(location.SizeBytes)}\n\n" +
                "Continue?",
                "Confirm storage cleanup",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning
            );

        if (confirmation !=
            MessageBoxResult.Yes)
        {
            return;
        }


        button.IsEnabled =
            false;

        AnalyzeStorageButton.IsEnabled =
            false;

        StorageStatusText.Text =
            $"Cleaning {location.Name}...";


        try
        {
            StorageCleanupResult result =
                await _storageCleaner
                    .CleanAsync(
                        location
                    );


            MessageBox.Show(
                $"Cleanup completed.\n\n" +
                $"Deleted: {FormatBytes(result.DeletedBytes)}\n" +
                $"Files deleted: {result.DeletedFiles:N0}\n" +
                $"Skipped: {result.SkippedFiles:N0}\n" +
                $"Failed: {result.FailedFiles:N0}",
                "Cleanup complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );


            await AnalyzeStorageAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "Storage cleanup error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );

            StorageStatusText.Text =
                "Cleanup failed";
        }
        finally
        {
            AnalyzeStorageButton.IsEnabled =
                true;
        }
    }

    private static string FormatBytes(
        long bytes)
    {
        const double kb =
            1024d;

        const double mb =
            kb * 1024d;

        const double gb =
            mb * 1024d;

        if (bytes >= gb)
        {
            return
                $"{bytes / gb:F2} GB";
        }

        if (bytes >= mb)
        {
            return
                $"{bytes / mb:F2} MB";
        }

        if (bytes >= kb)
        {
            return
                $"{bytes / kb:F2} KB";
        }

        return
            $"{bytes} B";
    }
}