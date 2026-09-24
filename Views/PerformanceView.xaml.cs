using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WinOptimizer.Models;
using WinOptimizer.Services;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace WinOptimizer.Views;

public partial class PerformanceView : UserControl
{
    private bool _initialLoadStarted;

    private readonly StartupAnalyzer _startupAnalyzer;
    private readonly StartupManager _startupManager;
    private readonly StartupHistoryManager _historyManager;

    public PerformanceView()
    {
        InitializeComponent();

        _startupAnalyzer =
            new StartupAnalyzer();

        _startupManager =
            new StartupManager();

        _historyManager =
            new StartupHistoryManager();

        Loaded += PerformanceView_Loaded;
        SelectPerformanceSection(PerformanceSection.Overview);
        SelectStartupDisplay(false);
    }

    private async void PerformanceView_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        if (_initialLoadStarted)
            return;
        _initialLoadStarted = true;
        await LoadStartupApplicationsAsync();
        if (IsLoaded)
            await RefreshMemoryAsync();
    }

}
