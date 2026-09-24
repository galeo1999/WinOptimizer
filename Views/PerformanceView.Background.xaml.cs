using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using WinOptimizer.Models;
using WinOptimizer.Services;

namespace WinOptimizer.Views;

public partial class PerformanceView
{
    private readonly BackgroundActivityAnalyzer _backgroundAnalyzer = new();
    private CancellationTokenSource? _backgroundCancellation;
    private BackgroundAnalysisResult? _backgroundResult;

    private async void RefreshBackground_Click(object sender, RoutedEventArgs e)
    {
        if (_backgroundCancellation != null) return;
        using var cancellation = new CancellationTokenSource();
        _backgroundCancellation = cancellation;
        RefreshBackgroundButton.IsEnabled = false;
        CancelBackgroundButton.IsEnabled = true;
        BackgroundStatus.Text = "Reading services and process counters… Previous snapshot remains visible until complete.";
        try
        {
            var result = await _backgroundAnalyzer.AnalyzeAsync(_memoryStartupItems, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            _backgroundResult = result;
            BackgroundSummary.DataContext = result;
            ApplyBackgroundView();
            RebuildBackgroundAdvisor();
            BackgroundStatus.Text = $"Measured {result.CapturedAt:G} · {result.UnavailableHosts} hosts without memory readings. {result.Warning}";
        }
        catch (OperationCanceledException)
        { BackgroundStatus.Text = "Cancelled. Any displayed results are from the previous snapshot."; }
        catch (Exception ex)
        { BackgroundStatus.Text = $"Unable to analyze background activity: {ex.Message}. Any displayed results are from the previous snapshot."; }
        finally
        {
            _backgroundCancellation = null;
            RefreshBackgroundButton.IsEnabled = true;
            CancelBackgroundButton.IsEnabled = false;
        }
    }

    private void CancelBackground_Click(object sender, RoutedEventArgs e) => _backgroundCancellation?.Cancel();
    private void BackgroundView_Changed(object sender, SelectionChangedEventArgs e) => ApplyBackgroundView();
    private void ApplyBackgroundView()
    {
        if (BackgroundList == null || BackgroundFilterBox == null || BackgroundSortBox == null) return;
        BackgroundList.ItemsSource = BackgroundActivityAnalyzer.FilterAndSort(_backgroundResult,
            (BackgroundFilter)BackgroundFilterBox.SelectedIndex, (BackgroundSort)BackgroundSortBox.SelectedIndex);
    }

    private void CopyServiceName_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: WindowsServiceInfo service }) return;
        try { Clipboard.SetText(service.ServiceName); }
        catch (Exception ex)
        { MessageBox.Show(Window.GetWindow(this), ex.Message, "Copy service name", MessageBoxButton.OK, MessageBoxImage.Warning); }
        e.Handled = true;
    }

    private void OpenServicesConsole_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string system = Environment.GetFolderPath(Environment.SpecialFolder.System);
            var start = new ProcessStartInfo(Path.Combine(system, "mmc.exe")) { UseShellExecute = true };
            start.ArgumentList.Add(Path.Combine(system, "services.msc"));
            Process.Start(start)?.Dispose();
        }
        catch (Exception ex)
        { MessageBox.Show(Window.GetWindow(this), ex.Message, "Windows Services", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
}
