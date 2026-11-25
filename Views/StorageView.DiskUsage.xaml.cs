using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WinOptimizer.Models;
using WinOptimizer.Services;

namespace WinOptimizer.Views;

public partial class StorageView
{
    private readonly DiskUsageAnalyzer _diskUsageAnalyzer = new();
    private CancellationTokenSource? _diskUsageCancellation;
    private string? _diskUsagePath;

    private void ChooseDiskUsageFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Choose a folder or drive to analyze",
            Multiselect = false
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true)
            return;

        if (string.Equals(_diskUsagePath, dialog.FolderName, StringComparison.OrdinalIgnoreCase))
            return;
        _diskUsagePath = dialog.FolderName;
        DiskUsagePathText.Text = _diskUsagePath;
        DiskUsageTree.ItemsSource = null;
        LargestFilesList.ItemsSource = null;
        DiskUsageTotalText.Text = "Not analyzed";
        DiskUsageStatusText.Text = "Ready to analyze the selected folder.";
        AnalyzeDiskUsageButton.IsEnabled = true;
    }

    private async void AnalyzeDiskUsage_Click(object sender, RoutedEventArgs e)
    {
        if (_diskUsageCancellation != null || _diskUsagePath == null)
            return;

        using var cancellation = new CancellationTokenSource();
        _diskUsageCancellation = cancellation;
        ChooseDiskUsageFolderButton.IsEnabled = false;
        AnalyzeDiskUsageButton.IsEnabled = false;
        CancelDiskUsageButton.Visibility = Visibility.Visible;
        CancelDiskUsageButton.IsEnabled = true;
        DiskUsageProgressBar.Visibility = Visibility.Visible;
        DiskUsageTree.ItemsSource = null;
        LargestFilesList.ItemsSource = null;
        DiskUsageTotalText.Text = "Analyzing...";
        DiskUsageStatusText.Text = "Scanning...";

        var progress = new Progress<DiskUsageProgress>(update =>
        {
            // Ignore queued updates after cancellation, completion or navigation away.
            if (_diskUsageCancellation != cancellation || cancellation.IsCancellationRequested)
                return;
            DiskUsageTotalText.Text = FormatBytes(update.SizeBytes);
            DiskUsageStatusText.Text = $"Scanning: {update.FileCount:N0} files · {update.SkippedItems:N0} skipped\n{update.CurrentPath}";
        });

        try
        {
            DiskUsageScanResult result = await _diskUsageAnalyzer.AnalyzeAsync(
                _diskUsagePath, cancellation.Token, progress);
            cancellation.Token.ThrowIfCancellationRequested();
            DiskUsageTree.ItemsSource = new[] { result.Root };
            LargestFilesList.ItemsSource = result.LargestFiles;
            DiskUsageTotalText.Text = FormatBytes(result.Root.SizeBytes);
            DiskUsageStatusText.Text = $"Analysis complete · {result.Root.FileCount:N0} files · " +
                $"{result.Root.SkippedItems:N0} skipped" +
                (result.Root.SkippedItems > 0 ? " (partial results)" : "");
        }
        catch (OperationCanceledException)
        {
            DiskUsageTotalText.Text = "Scan cancelled";
            DiskUsageStatusText.Text = "Scan cancelled. Run Analyze to start again.";
        }
        catch (Exception ex)
        {
            DiskUsageTotalText.Text = "Analysis failed";
            DiskUsageStatusText.Text = $"Unable to analyze the selected folder: {ex.Message}";
        }
        finally
        {
            _diskUsageCancellation = null;
            ChooseDiskUsageFolderButton.IsEnabled = true;
            AnalyzeDiskUsageButton.IsEnabled = true;
            CancelDiskUsageButton.Visibility = Visibility.Collapsed;
            DiskUsageProgressBar.Visibility = Visibility.Collapsed;
        }
    }

    private void CancelDiskUsage_Click(object sender, RoutedEventArgs e)
    {
        _diskUsageCancellation?.Cancel();
        CancelDiskUsageButton.IsEnabled = false;
        DiskUsageStatusText.Text = "Cancelling...";
    }

    private void StorageView_Unloaded(object sender, RoutedEventArgs e) =>
        _diskUsageCancellation?.Cancel();

    private void OpenDiskUsageFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;
        string? folder = button.Tag switch
        {
            StorageTreeNode node => node.Path,
            LargeFileInfo file => Path.GetDirectoryName(file.Path),
            _ => null
        };
        if (folder == null)
            return;
        try
        {
            if (!Path.IsPathFullyQualified(folder) || !Directory.Exists(folder))
                throw new DirectoryNotFoundException("This folder is no longer available or cannot be accessed.");
            var startInfo = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
            startInfo.ArgumentList.Add(folder);
            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            MessageBox.Show(Window.GetWindow(this), ex.Message, "Open Folder",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        e.Handled = true;
    }
}
