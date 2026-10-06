using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WinOptimizer.Services;

namespace WinOptimizer.Views;

public partial class RestoreView
{
    private void RefreshBackgroundHistory_Click(object sender, RoutedEventArgs e) => LoadBackgroundHistory();

    private void LoadBackgroundHistory()
    {
        BackgroundHistoryPanel.Children.Clear();
        try
        {
            var history = new BackgroundServiceManager().GetHistory();
            if (history.Count == 0)
                BackgroundHistoryPanel.Children.Add(CreateEmptyText("No background configuration restore points available."));
            foreach (var entry in history)
            {
                var card = CreateCard();
                var content = new StackPanel();
                content.Children.Add(new TextBlock { Text = entry.ServiceName, Foreground = Brushes.White,
                    FontSize = 17, FontWeight = FontWeights.SemiBold });
                content.Children.Add(CreateSecondaryText($"{entry.PreviousConfiguration?.StartupDescription ?? "Unknown"} → {entry.NewConfiguration?.StartupDescription ?? "Unknown"}", 5));
                content.Children.Add(CreateSecondaryText($"{entry.Timestamp:G} · {entry.Action}", 5));
                content.Children.Add(CreateSecondaryText($"Status: {entry.Status} · Restore: {entry.RestoreStatus}", 5));
                if (entry.RestoredAt.HasValue) content.Children.Add(CreateSecondaryText($"Restored: {entry.RestoredAt:G}", 5));
                if (entry.Error != null) content.Children.Add(CreateSecondaryText(entry.Error, 5));
                if (entry.RollbackStatus != null) content.Children.Add(CreateSecondaryText(entry.RollbackStatus, 5));
                content.Children.Add(CreateSecondaryText("Restores startup configuration, including delayed automatic start. Running state is recorded but never changed.", 5));
                var button = new Button { Content = entry.Status == "Restored" ? "Restored" : "Review Restore",
                    Tag = entry.Id, IsEnabled = entry.Status != "Restored", HorizontalAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(15, 8, 15, 8), Margin = new Thickness(0, 12, 0, 0) };
                button.Click += BackgroundRestore_Click;
                content.Children.Add(button);
                card.Child = content;
                BackgroundHistoryPanel.Children.Add(card);
            }
        }
        catch (Exception ex) { BackgroundHistoryPanel.Children.Add(CreateEmptyText("Background history unavailable: " + ex.Message)); }
    }

    private async void BackgroundRestore_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: Guid id } button) return;
        button.IsEnabled = false;
        try { await BackgroundServiceActionWorkflow.RunAsync(Window.GetWindow(this), null, id); }
        finally { LoadBackgroundHistory(); }
    }
}
