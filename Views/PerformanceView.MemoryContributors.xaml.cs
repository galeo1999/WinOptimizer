using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using WinOptimizer.Models;
using WinOptimizer.Services;

namespace WinOptimizer.Views;

public partial class PerformanceView
{
    private IReadOnlyList<MemoryApplicationGroup> _memoryApplicationGroups = [];

    private void RebuildMemoryContributors()
    {
        _memoryApplicationGroups = _latestMemoryResult == null ? []
            : MemoryContributorAnalyzer.Analyze(_latestMemoryResult.Processes);
        LargestContributorPanel.DataContext = MemoryContributorAnalyzer.LargestWorkingSet(_memoryApplicationGroups);
        ContributorStatusText.Text = _latestMemoryResult == null
            ? "Refresh Memory to collect a current snapshot."
            : $"Measured {_latestMemoryResult.CapturedAt:G} · {_memoryApplicationGroups.Count:N0} executable/PID groups · " +
              $"{_memoryApplicationGroups.Count(group => group.UnavailableProcessCount > 0):N0} groups with partial readings";
        ApplyContributorSort();
    }

    private void ContributorSort_Changed(object sender, SelectionChangedEventArgs e) => ApplyContributorSort();

    private void ApplyContributorSort()
    {
        if (MemoryContributorsTree == null || ContributorSort == null)
            return;
        MemoryContributorsTree.ItemsSource = MemoryContributorAnalyzer.Sort(
            _memoryApplicationGroups, (MemoryContributorSort)ContributorSort.SelectedIndex);
    }

    private void OpenProcessLocation_Click(object sender, RoutedEventArgs e)
    {
        string? executable = (sender as Button)?.Tag switch
        {
            ProcessMemoryInfo process => process.ExecutablePath,
            MemoryApplicationGroup group => group.ExecutablePath,
            _ => null
        };
        try
        {
            if (string.IsNullOrWhiteSpace(executable) || !Path.IsPathFullyQualified(executable))
                throw new IOException("The executable path is unavailable.");
            string? folder = Path.GetDirectoryName(executable);
            if (folder == null || !Directory.Exists(folder))
                throw new DirectoryNotFoundException("The process location is no longer available or cannot be accessed.");
            var start = new ProcessStartInfo(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"))
            {
                UseShellExecute = true
            };
            start.ArgumentList.Add(folder);
            Process.Start(start)?.Dispose();
        }
        catch (Exception ex)
        {
            MessageBox.Show(Window.GetWindow(this), ex.Message, "Open Process Location",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        e.Handled = true;
    }

    private void OpenMemoryTaskManager_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System), "Taskmgr.exe"))
            {
                UseShellExecute = true
            })?.Dispose();
        }
        catch (Exception ex)
        {
            MessageBox.Show(Window.GetWindow(this), ex.Message, "Open Task Manager",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void CopyMemoryPid_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ProcessMemoryInfo process })
            return;
        try { Clipboard.SetText(process.ProcessId.ToString(CultureInfo.InvariantCulture)); }
        catch (Exception ex)
        {
            MessageBox.Show(Window.GetWindow(this), ex.Message, "Copy PID",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        e.Handled = true;
    }
}
