using System.Windows;
using System.Windows.Controls;
using WinOptimizer.Models;
using WinOptimizer.Services;

namespace WinOptimizer.Views;

public partial class PerformanceView
{
    private BackgroundOptimizationAdvice? _backgroundAdvice;

    private void RebuildBackgroundAdvisor()
    {
        if (BackgroundAdvisorSummary == null) return;
        _backgroundAdvice = _backgroundResult == null ? null
            : BackgroundOptimizationAdvisor.Analyze(_backgroundResult, _memoryStartupScanAvailable ? _memoryStartupItems : []);
        BackgroundAdvisorSummary.DataContext = _backgroundAdvice;
        BackgroundAdvisorStatus.Text = _backgroundAdvice == null ? "Refresh Background to collect a measurement."
            : $"Measured {_backgroundAdvice.CapturedAt:G} · {_backgroundAdvice.ReviewableProcessCount} reviewable PIDs · " +
              $"{_backgroundAdvice.UnavailableWorkingSetCount} unavailable working sets. " +
              (!_memoryStartupScanAvailable ? "Startup data unavailable. " : "") + _backgroundResult?.Warning;
        ApplyBackgroundAdvisorFilter();
    }

    private void BackgroundAdvisorFilter_Changed(object sender, SelectionChangedEventArgs e) => ApplyBackgroundAdvisorFilter();
    private void ApplyBackgroundAdvisorFilter()
    {
        if (BackgroundAdvisorRows == null || BackgroundAdvisorFilterBox == null) return;
        BackgroundAdvisorRows.ItemsSource = BackgroundOptimizationAdvisor.Filter(_backgroundAdvice,
            (BackgroundAdvisorFilter)BackgroundAdvisorFilterBox.SelectedIndex);
    }

    private void BackgroundDisplay_Click(object sender, RoutedEventArgs e)
    {
        bool advisor = ReferenceEquals(sender, BackgroundOpportunitiesTab);
        BackgroundActivityTab.IsChecked = !advisor;
        BackgroundOpportunitiesTab.IsChecked = advisor;
        BackgroundSummaryCard.Visibility = BackgroundOptions.Visibility = BackgroundList.Visibility = BackgroundNotes.Visibility
            = advisor ? Visibility.Collapsed : Visibility.Visible;
        BackgroundAdvisorPanel.Visibility = advisor ? Visibility.Visible : Visibility.Collapsed;
    }

    private void NavigateBackgroundStartup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: BackgroundOptimizationRecommendation row } || !row.CanManageStartup) return;
        // Revalidate against current startup data rather than trusting a potentially stale card.
        var matches = StartupProcessMatcher.FindMatches(row.Activity.Process?.ExecutablePath,
            StartupProcessMatcher.CreateIndex(_memoryStartupItems));
        if (matches.Count != 1)
        {
            RebuildBackgroundAdvisor();
            BackgroundAdvisorStatus.Text = "Startup association changed; review the updated results.";
            return;
        }
        ViewAdvisorStartup_Click(sender, e);
        StartupAppsPanel.Children.OfType<Border>().FirstOrDefault(card => ReferenceEquals(card.Tag, matches[0]))?.BringIntoView();
    }
    private async void ReviewBackgroundChange_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: BackgroundOptimizationRecommendation { SupportedAction: { } action } }) return;
        if (_backgroundCancellation != null)
        {
            BackgroundAdvisorStatus.Text = "Wait for the current background scan to finish before reviewing a change.";
            return;
        }
        BackgroundSection.IsEnabled = false;
        try
        {
            int result = await BackgroundServiceActionWorkflow.RunAsync(Window.GetWindow(this), action.Id, null);
            if (result != 2 && IsLoaded) RefreshBackground_Click(sender, e);
        }
        finally { BackgroundSection.IsEnabled = true; }
    }

}
