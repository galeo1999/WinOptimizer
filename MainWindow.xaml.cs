using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WinOptimizer.Services;
using WinOptimizer.Views;
namespace WinOptimizer
{
    public partial class MainWindow : Window
    {
   public MainWindow(
    bool openPrivacy = false,
    bool openPerformance = false)
{
    InitializeComponent();

    UpdatePrivilegeStatus();

    if (openPrivacy)
    {
        NavigateTo(
            new PrivacyView(),
            PrivacyButton
        );
    }
    else if (openPerformance)
    {
        NavigateTo(
            new PerformanceView(),
            PerformanceButton
        );
    }
    else
    {
        NavigateTo(
            new DashboardView(),
            DashboardButton
        );
    }
}
private void UpdatePrivilegeStatus()
{
    bool isAdmin =
        AdminService.IsRunningAsAdministrator();

    if (isAdmin)
    {
        PrivilegeStatusText.Text =
            "● Administrator";

        PrivilegeStatusText.Foreground =
            new SolidColorBrush(
                Color.FromRgb(34, 197, 94)
            );
    }
    else
    {
        PrivilegeStatusText.Text =
            "● Standard User";

        PrivilegeStatusText.Foreground =
            new SolidColorBrush(
                Color.FromRgb(100, 116, 139)
            );
    }
}

        private void NavigateTo(
            UserControl view,
            Button activeButton)
        {
            MainContent.Content = view;

            ResetSidebarButtons();

            activeButton.Tag = "Active";
        }

        private void ResetSidebarButtons()
        {
            DashboardButton.Tag = null;
            PrivacyButton.Tag = null;
            PerformanceButton.Tag = null;
            StorageButton.Tag = null;
            DebloatButton.Tag = null;
            RestoreButton.Tag = null;
        }

        private void DashboardButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            NavigateTo(
                new DashboardView(),
                DashboardButton
            );
        }

        private void PrivacyButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            NavigateTo(
                new PrivacyView(),
                PrivacyButton
            );
        }

        private void PerformanceButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            NavigateTo(
                new PerformanceView(),
                PerformanceButton
            );
        }

        private void StorageButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            NavigateTo(
                new StorageView(),
                StorageButton
            );
        }

        private void DebloatButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            NavigateTo(
                new DebloatView(),
                DebloatButton
            );
        }

        private void RestoreButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            NavigateTo(
                new RestoreView(),
                RestoreButton
            );
        }
    }
}