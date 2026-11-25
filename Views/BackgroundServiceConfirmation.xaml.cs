using System.Windows;
using WinOptimizer.Models;

namespace WinOptimizer.Views;

public partial class BackgroundServiceConfirmation : Window
{
    public BackgroundServiceConfirmation(BackgroundServiceChangePlan plan)
    {
        InitializeComponent();
        ActionName.Text = $"{plan.Action.DisplayName} ({plan.Action.ServiceName})";
        ChangeDetails.Text = $"Current startup type: {plan.Current.StartupDescription}\nProposed startup type: {plan.Target.StartupDescription}\n" +
            $"Current service state: {(plan.Current.RunningState == 4 ? "Running" : plan.Current.RunningState == 1 ? "Stopped" : "Transitioning / other")}";
        EffectText.Text = plan.RestoreId.HasValue
            ? "Restore the exact previous startup configuration from this backup, including delayed automatic start. Running state is not restored."
            : plan.Action.Description;
        Consequence.Text = plan.RestoreId.HasValue
            ? "The service may start automatically at a subsequent boot according to the backed-up configuration. This does not start it now."
            : plan.Action.Consequence;
        ApplyButton.Content = plan.RestoreId.HasValue ? "Restore" : "Apply";
    }
    private void Apply_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
