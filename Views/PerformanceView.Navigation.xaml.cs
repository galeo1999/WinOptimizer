using System.Windows;
using WinOptimizer.Models;
using WinOptimizer.Services;

namespace WinOptimizer.Views;

public enum PerformanceSection { Overview, Memory, Processes, Startup, Background }

public partial class PerformanceView
{
    private PerformanceSection? _selectedPerformanceSection;
    private bool _refreshAllInProgress;

    private void NavigatePerformance_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: PerformanceSection section })
            SelectPerformanceSection(section);
    }

    private void SelectPerformanceSection(PerformanceSection section)
    {
        if (_selectedPerformanceSection == section)
            return;
        _selectedPerformanceSection = section;
        OverviewSection.Visibility = section == PerformanceSection.Overview ? Visibility.Visible : Visibility.Collapsed;
        MemorySection.Visibility = section == PerformanceSection.Memory ? Visibility.Visible : Visibility.Collapsed;
        ProcessesSection.Visibility = section == PerformanceSection.Processes ? Visibility.Visible : Visibility.Collapsed;
        StartupSection.Visibility = section == PerformanceSection.Startup ? Visibility.Visible : Visibility.Collapsed;
        BackgroundSection.Visibility = section == PerformanceSection.Background ? Visibility.Visible : Visibility.Collapsed;
        BackgroundTab.IsChecked = section == PerformanceSection.Background;
        OverviewTab.IsChecked = section == PerformanceSection.Overview;
        MemoryTab.IsChecked = section == PerformanceSection.Memory;
        ProcessesTab.IsChecked = section == PerformanceSection.Processes;
        StartupTab.IsChecked = section == PerformanceSection.Startup;
        // Keep the controls and their item sources in place. No loading, rebinding or scans here.
    }

    private void StartupDisplay_Click(object sender, RoutedEventArgs e) =>
        SelectStartupDisplay(ReferenceEquals(sender, StartupOpportunitiesButton));

    private void SelectStartupDisplay(bool showOpportunities)
    {
        StartupApplicationsPanel.Visibility = showOpportunities ? Visibility.Collapsed : Visibility.Visible;
        StartupOpportunitiesPanel.Visibility = showOpportunities ? Visibility.Visible : Visibility.Collapsed;
        StartupApplicationsButton.IsChecked = !showOpportunities;
        StartupOpportunitiesButton.IsChecked = showOpportunities;
    }

    private async void RefreshPerformanceOverview_Click(object sender, RoutedEventArgs e)
    {
        if (_refreshAllInProgress || _memoryCancellation != null)
            return;
        _refreshAllInProgress = true;
        RefreshOverviewButton.IsEnabled = false;
        try
        {
            await LoadStartupApplicationsAsync();
            if (IsLoaded)
                await RefreshMemoryAsync();
        }
        finally
        {
            _refreshAllInProgress = false;
            RefreshOverviewButton.IsEnabled = true;
        }
    }

    private void UpdatePerformanceOverview()
    {
        OverviewCards.DataContext = new PerformanceOverviewSummary(
            _latestMemoryResult?.Snapshot,
            MemoryContributorAnalyzer.LargestWorkingSet(_memoryApplicationGroups),
            _latestMemoryResult?.Processes.Count,
            _memoryStartupScanAvailable ? _memoryStartupItems.Count : null,
            _memoryStartupScanAvailable ? _memoryStartupItems.Count(item => item.State == StartupState.Enabled) : null,
            _memoryStartupScanAvailable ? _memoryStartupItems.Count(item => item.State == StartupState.Disabled) : null,
            _memoryStartupScanAvailable ? _memoryStartupItems.Count(item => item.State == StartupState.Unknown) : null,
            _memoryAdvice,
            MemoryContributorAnalyzer.Sort(_memoryApplicationGroups, MemoryContributorSort.WorkingSet).Where(group => group.CombinedWorkingSetBytes.HasValue).Take(2).ToArray());
    }
}

// UI projection only: consumes the same snapshots as the detailed sections.
public sealed record PerformanceOverviewSummary(
    MemorySnapshot? Memory,
    MemoryApplicationGroup? LargestContributor,
    int? ProcessCount,
    int? StartupCount,
    int? EnabledStartupCount,
    int? DisabledStartupCount,
    int? UnknownStartupCount,
    MemoryOptimizationAdvice? StartupMemory,
    IReadOnlyList<MemoryApplicationGroup> TopApplications);
