using System.Globalization;
using System.Windows;
using System.Windows.Data;
using WinOptimizer.Models;
using WinOptimizer.Services;

namespace WinOptimizer.Views;

public partial class PerformanceView
{
    private readonly MemoryAnalyzer _memoryAnalyzer = new();
    private IReadOnlyList<StartupItem> _memoryStartupItems = [];
    private CancellationTokenSource? _memoryCancellation;

    private async void RefreshMemory_Click(object sender, RoutedEventArgs e)
    {
        if (_advisorRefreshInProgress || _memoryCancellation != null)
            return;
        _advisorRefreshInProgress = true;
        RefreshMemoryButton.IsEnabled = false;
        try
        {
            await LoadStartupApplicationsAsync();
            if (IsLoaded)
                await RefreshMemoryAsync();
        }
        finally
        {
            _advisorRefreshInProgress = false;
            RefreshMemoryButton.IsEnabled = true;
        }
    }

    private async Task RefreshMemoryAsync()
    {
        if (_memoryCancellation != null)
            return;
        using var cancellation = new CancellationTokenSource();
        _memoryCancellation = cancellation;
        RefreshMemoryButton.IsEnabled = false;
        MemoryStatusText.Text = "Reading memory and process information...";
        MemoryBusyBar.Visibility = Visibility.Visible;
        // Remove stale readings so they cannot be mistaken for the current sample after a failure.
        MemorySummaryPanel.DataContext = null;
        MemoryProcessesGrid.ItemsSource = null;
        _latestMemoryResult = null;
        RebuildMemoryAdvisor();
        try
        {
            MemoryAnalysisResult result = await _memoryAnalyzer.AnalyzeAsync(_memoryStartupItems, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            MemorySummaryPanel.DataContext = result.Snapshot;
            MemoryProcessesGrid.ItemsSource = result.Processes;
            _latestMemoryResult = result;
            RebuildMemoryAdvisor();
            int inaccessible = result.Processes.Count(item => !item.IsAccessible);
            MemoryStatusText.Text = $"Updated {result.CapturedAt:HH:mm:ss} · {result.Processes.Count:N0} processes · " +
                $"{inaccessible:N0} unavailable/exited · sorted by working set";
        }
        catch (OperationCanceledException)
        {
            MemoryStatusText.Text = "Memory scan cancelled. Refresh to try again.";
        }
        catch (Exception ex)
        {
            MemoryStatusText.Text = $"Unable to read memory information: {ex.Message}";
        }
        finally
        {
            _memoryCancellation = null;
            RefreshMemoryButton.IsEnabled = true;
            MemoryBusyBar.Visibility = Visibility.Collapsed;
        }
    }

    private void PerformanceMemory_Unloaded(object sender, RoutedEventArgs e) =>
        _memoryCancellation?.Cancel();
}

public sealed class MemoryBytesConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || value == DependencyProperty.UnsetValue)
            return "Unavailable";
        double bytes = System.Convert.ToDouble(value, culture);
        if (bytes >= 1024d * 1024 * 1024)
            return (bytes / (1024d * 1024 * 1024)).ToString("N2", culture) + " GiB";
        if (bytes >= 1024d * 1024)
            return (bytes / (1024d * 1024)).ToString("N1", culture) + " MiB";
        if (bytes >= 1024d)
            return (bytes / 1024d).ToString("N1", culture) + " KiB";
        return bytes.ToString("N0", culture) + " B";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
