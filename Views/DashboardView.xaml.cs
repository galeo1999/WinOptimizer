using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WinOptimizer.Models;
using WinOptimizer.Services;

namespace WinOptimizer.Views
{
    public partial class DashboardView : UserControl
    {
        private readonly SystemAnalyzer _analyzer;
        private readonly DispatcherTimer _systemTimer;

        public DashboardView()
        {
            InitializeComponent();

            _analyzer = new SystemAnalyzer();

            _systemTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };

            _systemTimer.Tick += SystemTimer_Tick;
            Loaded += DashboardView_Loaded;
            Unloaded += DashboardView_Unloaded;
        }

        private void DashboardView_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            LoadSystemInfo();
            UpdateRamInfo();
            _analyzer.GetCpuUsagePercentage();

            _systemTimer.Start();
        }

        private void DashboardView_Unloaded(
            object sender,
            RoutedEventArgs e)
        {
            _systemTimer.Stop();
        }

        private void SystemTimer_Tick(
            object? sender,
            EventArgs e)
        {
            UpdateRamInfo();
            UpdateCpuInfo();
        }

        private void LoadSystemInfo()
        {
            try
            {
                // Windows
                WindowsVersionText.Text =
                    _analyzer.GetWindowsVersion();

                // CPU
                CpuText.Text =
                    $"{_analyzer.GetProcessorName()}\n" +
                    $"{_analyzer.GetProcessorCount()} logical processors";

                // Motherboard
                MotherboardText.Text =
                    _analyzer.GetMotherboardName();

                // BIOS
                BiosText.Text =
                    _analyzer.GetBiosVersion();

                // System Drive
                DriveInfo systemDrive =
                    _analyzer.GetSystemDrive();

                SystemDriveText.Text =
                    systemDrive.Name;

                // GPU
                var gpus = _analyzer.GetGpus();

                GpuText.Text = string.Join(
                    "\n\n",
                    gpus.Select(gpu =>
                        $"{gpu.Name}\n" +
                        $"Driver: {gpu.DriverVersion}"
                    )
                );
                // Physical Drives
          
                    LoadPhysicalDrives();
                   LoadVolumes();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.ToString(),
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private void UpdateRamInfo()
        {
            MemorySnapshot memory =
                _analyzer.GetMemorySnapshot();

            RamDetailsText.Text =
                $"Used: {memory.UsedGigabytes:F2} GB / " +
                $"{memory.TotalGigabytes:F2} GB\n" +
                $"Available: {memory.AvailableGigabytes:F2} GB";

            RamProgressBar.Value =
                memory.UsagePercentage;

            RamPercentText.Text =
                $"{memory.UsagePercentage} % in use";

            if (memory.UsagePercentage >= 85)
            {
                RamProgressBar.Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(239, 68, 68)
                    );
            }
            else if (memory.UsagePercentage >= 70)
            {
                RamProgressBar.Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(245, 158, 11)
                    );
            }
            else
            {
                RamProgressBar.Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(34, 197, 94)
                    );
            }
        }
        private void LoadVolumes()
        {
            VolumesPanel.Children.Clear();

            foreach (DriveInfo drive in _analyzer.GetDrives())
            {
                double totalSpace =
                    BytesToGigabytes(
                        (ulong)drive.TotalSize
                    );

                double freeSpace =
                    BytesToGigabytes(
                        (ulong)drive.AvailableFreeSpace
                    );

                double usedSpace =
                    totalSpace - freeSpace;

                double usagePercentage =
                    totalSpace > 0
                        ? usedSpace / totalSpace * 100
                        : 0;

                Border card =
                    CreateVolumeCard(
                        drive,
                        totalSpace,
                        usedSpace,
                        freeSpace,
                        usagePercentage
                    );

                VolumesPanel.Children.Add(card);
            }
        }
        private Border CreateVolumeCard(
            DriveInfo drive,
            double totalSpace,
            double usedSpace,
            double freeSpace,
            double usagePercentage)
        {
            Border card = new Border
            {
                Background =
                    new SolidColorBrush(
                        Color.FromRgb(15, 23, 42)
                    ),

                CornerRadius = new CornerRadius(10),

                Padding = new Thickness(15),

                Margin = new Thickness(
                    0,
                    0,
                    0,
                    12
                )
            };

            StackPanel content =
                new StackPanel();

            // Laufwerksname

            TextBlock driveName =
                new TextBlock
                {
                    Text = drive.Name,
                    Foreground = Brushes.White,
                    FontSize = 17,
                    FontWeight = FontWeights.SemiBold
                };

            content.Children.Add(driveName);

            // Dateisystem

            TextBlock driveFormat =
                new TextBlock
                {
                    Text =
                        $"{drive.DriveFormat} • " +
                        $"{drive.DriveType}",

                    Foreground =
                        new SolidColorBrush(
                            Color.FromRgb(148, 163, 184)
                        ),

                    FontSize = 12,

                    Margin =
                        new Thickness(
                            0,
                            3,
                            0,
                            12
                        )
                };

            content.Children.Add(driveFormat);

            // Used / Total

            TextBlock storageText =
                new TextBlock
                {
                    Text =
                        $"{usedSpace:F2} GB used / " +
                        $"{totalSpace:F2} GB",

                    Foreground =
                        new SolidColorBrush(
                            Color.FromRgb(209, 213, 219)
                        ),

                    FontSize = 13
                };

            content.Children.Add(storageText);

            // ProgressBar

            ProgressBar progressBar =
                new ProgressBar
                {
                    Minimum = 0,
                    Maximum = 100,
                    Value = usagePercentage,

                    Margin =
                        new Thickness(
                            0,
                            10,
                            0,
                            8
                        )
                };

            progressBar.Style =
                (Style)FindResource(
                    "ModernProgressBarStyle"
                );

            // Farbe abhängig von Speicherverbrauch

            if (usagePercentage >= 90)
            {
                progressBar.Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(239, 68, 68)
                    );
            }
            else if (usagePercentage >= 75)
            {
                progressBar.Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(245, 158, 11)
                    );
            }
            else
            {
                progressBar.Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(34, 197, 94)
                    );
            }

            content.Children.Add(progressBar);

            // Prozent + freier Speicher

            TextBlock freeText =
                new TextBlock
                {
                    Text =
                        $"{usagePercentage:F0} % used • " +
                        $"{freeSpace:F2} GB free",

                    Foreground =
                        new SolidColorBrush(
                            Color.FromRgb(148, 163, 184)
                        ),

                    FontSize = 12
                };

            content.Children.Add(freeText);

            card.Child = content;

            return card;
        }

        private void UpdateCpuInfo()
        {
            double cpuUsage =
                _analyzer.GetCpuUsagePercentage();

            CpuProgressBar.Value = cpuUsage;

            CpuPercentText.Text =
                $"{cpuUsage:F0} % in use";

            if (cpuUsage >= 85)
            {
                CpuProgressBar.Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(239, 68, 68)
                    );
            }
            else if (cpuUsage >= 70)
            {
                CpuProgressBar.Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(245, 158, 11)
                    );
            }
            else
            {
                CpuProgressBar.Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(34, 197, 94)
                    );
            }
        }
        private void LoadPhysicalDrives()
{
    PhysicalDrivesPanel.Children.Clear();

    foreach (var drive in _analyzer.GetPhysicalDrives())
    {
        double capacity =
            BytesToGigabytes(drive.Size);

        Border card = new Border
        {
            Background = new SolidColorBrush(
                Color.FromRgb(15, 23, 42)
            ),

            CornerRadius = new CornerRadius(10),

            Padding = new Thickness(15),

            Margin = new Thickness(
                0,
                0,
                0,
                10
            )
        };

        StackPanel content = new StackPanel();

        TextBlock modelText = new TextBlock
        {
            Text = drive.Model,
            Foreground = Brushes.White,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };

        TextBlock capacityText = new TextBlock
        {
            Text = $"Capacity: {capacity:F2} GB",
            Foreground = new SolidColorBrush(
                Color.FromRgb(209, 213, 219)
            ),
            FontSize = 13,
            Margin = new Thickness(0, 8, 0, 0)
        };

        TextBlock firmwareText = new TextBlock
        {
            Text = $"Firmware: {drive.Firmware}",
            Foreground = new SolidColorBrush(
                Color.FromRgb(148, 163, 184)
            ),
            FontSize = 12,
            Margin = new Thickness(0, 4, 0, 0)
        };

        TextBlock interfaceText = new TextBlock
        {
            Text = $"Interface: {drive.InterfaceType}",
            Foreground = new SolidColorBrush(
                Color.FromRgb(148, 163, 184)
            ),
            FontSize = 12,
            Margin = new Thickness(0, 3, 0, 0)
        };

        content.Children.Add(modelText);
        content.Children.Add(capacityText);
        content.Children.Add(firmwareText);
        content.Children.Add(interfaceText);

        card.Child = content;

        PhysicalDrivesPanel.Children.Add(card);
    }
}

        private static double BytesToGigabytes(
            ulong bytes)
        {
            return bytes
                / 1024.0
                / 1024.0
                / 1024.0;
        }
    }
}