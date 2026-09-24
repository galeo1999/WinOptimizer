using System.Windows;
using System.Windows.Controls;
using WinOptimizer.Models;
using WinOptimizer.Services;

namespace WinOptimizer.Views;

public partial class PerformanceView
{
    private MemoryAnalysisResult? _latestMemoryResult;
    private MemoryOptimizationAdvice? _memoryAdvice;
    private bool _memoryStartupScanAvailable;

    private void RebuildMemoryAdvisor()
    {
        _memoryAdvice = _latestMemoryResult != null && _memoryStartupScanAvailable
            ? MemoryOptimizationAdvisor.Analyze(_latestMemoryResult, _memoryStartupItems)
            : null;
        AdvisorSummaryPanel.DataContext = _memoryAdvice;
        if (_memoryAdvice == null)
        {
            AdvisorStatusText.Text = !_memoryStartupScanAvailable
                ? "Startup information is not available yet. Refresh Memory to retry."
                : "Refresh Memory to collect a current measurement.";
        }
        else
        {
            AdvisorStatusText.Text = $"Memory measured {_memoryAdvice.CapturedAt:G} · " +
                $"{_memoryAdvice.MatchedProcessCount:N0} uniquely matched processes · " +
                $"{_memoryAdvice.UnavailableProcessCount:N0} with unavailable memory readings" +
                (_memoryAdvice.UnavailableProcessCount > 0 ? " (partial totals)" : "");
            // Update associations after a startup change without scanning processes again.
            foreach (var row in _memoryAdvice.Recommendations)
                row.Process.StartupAssociation = StartupProcessMatcher.Describe(row.StartupMatches);
            MemoryProcessesGrid.Items.Refresh();
        }
        ApplyMemoryAdvisorOptions();
        RebuildMemoryContributors();
        UpdatePerformanceOverview();
    }

    private void MemoryAdvisorOptions_Changed(object sender, SelectionChangedEventArgs e) =>
        ApplyMemoryAdvisorOptions();

    private void ApplyMemoryAdvisorOptions()
    {
        // SelectionChanged can fire while InitializeComponent is still creating controls.
        if (AdvisorRows == null || AdvisorFilter == null || AdvisorSort == null)
            return;
        var rows = _memoryAdvice == null ? [] : MemoryOptimizationAdvisor.FilterAndSort(
            _memoryAdvice, (MemoryAdvisorFilter)AdvisorFilter.SelectedIndex,
            (MemoryAdvisorSort)AdvisorSort.SelectedIndex);
        AdvisorRows.ItemsSource = rows;
        AdvisorRowsStatusText.Text = $"{rows.Count:N0} processes shown" +
            (rows.Count == 0 ? " · No results for the current snapshot and filter." : "");
    }

    private void ManageAdvisorStartup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: MemoryOptimizationRecommendation row } || !row.CanManageStartup)
            return;
        // Navigate to the existing card. Its original button retains confirmation,
        // StartupManager history/backup, UAC handling and restore compatibility.
        Border? card = StartupAppsPanel.Children.OfType<Border>()
            .FirstOrDefault(candidate => ReferenceEquals(candidate.Tag, row.StartupEntry));
        if (card == null)
        {
            AdvisorStatusText.Text = "Startup information changed. Refresh Memory before managing this entry.";
            return;
        }
        SelectPerformanceSection(PerformanceSection.Startup);
        SelectStartupDisplay(false);
        StartupSection.UpdateLayout();
        card.BringIntoView();
        if (card.Child is StackPanel content)
            content.Children.OfType<Button>().FirstOrDefault()?.Focus();
    }

    private void ViewAdvisorStartup_Click(object sender, RoutedEventArgs e)
    {
        SelectPerformanceSection(PerformanceSection.Startup);
        SelectStartupDisplay(false);
        StartupSection.UpdateLayout();
        StartupAppsPanel.BringIntoView();
    }
}
