using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WinOptimizer.Models;
using WinOptimizer.Services;

namespace WinOptimizer.Views;

public partial class PrivacyView : UserControl
{
    private readonly TweakEngine _tweakEngine;

    private List<TweakDefinition> _tweaks = [];

    public PrivacyView()
    {
        InitializeComponent();

        _tweakEngine =
            new TweakEngine();

        Loaded += PrivacyView_Loaded;
    }

    private void PrivacyView_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        LoadPrivacyTweaks();
    }

    private void LoadPrivacyTweaks()
    {
        try
        {
            PrivacyTweaksPanel.Children.Clear();

            _tweaks =
                _tweakEngine.LoadTweaks(
                    "privacy.json"
                );

            foreach (TweakDefinition tweak in _tweaks)
            {
                PrivacyTweaksPanel.Children.Add(
                    CreateTweakCard(tweak)
                );
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "Privacy detection error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }

    private Border CreateTweakCard(
        TweakDefinition tweak)
    {
        TweakState state =
            _tweakEngine.GetState(tweak);

        Border card =
            new Border
            {
                Background =
                    new SolidColorBrush(
                        Color.FromRgb(31, 41, 55)
                    ),

                CornerRadius =
                    new CornerRadius(12),

                Padding =
                    new Thickness(20),

                Margin =
                    new Thickness(
                        0,
                        0,
                        0,
                        15
                    )
            };

        Grid grid =
            new Grid();

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(
                    1,
                    GridUnitType.Star
                )
            }
        );

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            }
        );

        StackPanel information =
            new StackPanel();

        // Name
        TextBlock nameText =
            new TextBlock
            {
                Text = tweak.Name,
                Foreground = Brushes.White,
                FontSize = 17,
                FontWeight =
                    FontWeights.SemiBold
            };

        information.Children.Add(
            nameText
        );

        // Description
        TextBlock descriptionText =
            new TextBlock
            {
                Text = tweak.Description,

                Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(
                            148,
                            163,
                            184
                        )
                    ),

                FontSize = 13,

                TextWrapping =
                    TextWrapping.Wrap,

                Margin =
                    new Thickness(
                        0,
                        5,
                        20,
                        0
                    )
            };

        information.Children.Add(
            descriptionText
        );

        // Metadata
        TextBlock metadataText =
            new TextBlock
            {
             Text =
    $"Risk: {tweak.Risk}" +
    (tweak.RequiresAdmin
        ? " • Administrator required"
        : "") +
    (tweak.RequiresRestart
        ? " • Restart required"
        : ""),

                Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(
                            100,
                            116,
                            139
                        )
                    ),

                FontSize = 11,

                Margin =
                    new Thickness(
                        0,
                        8,
                        0,
                        0
                    )
            };

        information.Children.Add(
            metadataText
        );

        // Status
        TextBlock statusText =
            new TextBlock
            {
                Text =
                    GetStateText(state),

                Foreground =
                    GetStateColor(state),

                FontSize = 13,

                Margin =
                    new Thickness(
                        0,
                        12,
                        0,
                        0
                    )
            };

        information.Children.Add(
            statusText
        );

        Grid.SetColumn(
            information,
            0
        );

        grid.Children.Add(
            information
        );

        // Button
        Button actionButton =
            new Button
            {
                Content =
                    GetButtonText(state),

                Padding =
                    new Thickness(
                        15,
                        8,
                        15,
                        8
                    ),

                Margin =
                    new Thickness(
                        20,
                        0,
                        0,
                        0
                    ),

                VerticalAlignment =
                    VerticalAlignment.Center,

                IsEnabled =
                    state != TweakState.Applied,

                Tag = tweak.Id
            };

        actionButton.Click +=
            TweakButton_Click;

        Grid.SetColumn(
            actionButton,
            1
        );

        grid.Children.Add(
            actionButton
        );

        card.Child =
            grid;

        return card;
    }

    private void TweakButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        
        if (sender is not Button button ||
            button.Tag is not string tweakId)
        {
            return;
        }

        TweakDefinition? tweak =
            _tweaks.Find(
                item =>
                    item.Id == tweakId
            );

        if (tweak == null)
        {
            return;
        }
        if (tweak.RequiresAdmin &&
    !AdminService.IsRunningAsAdministrator())
{
    MessageBoxResult result =
        MessageBox.Show(
            "This setting requires administrator privileges.\n\n" +
            "Restart WinOptimizer as administrator?",
            "Administrator privileges required",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information
        );

    if (result != MessageBoxResult.Yes)
    {
        return;
    }

  bool started =
    AdminService.TryStartElevatedInstance(
        $"--apply-tweak {tweak.Id}"
    );

    if (started)
    {
        Application.Current.Shutdown();
    }

    return;
}

        try
        {
            _tweakEngine.ApplyTweak(
                tweak
            );

            // UI komplett neu aus Registry lesen
            LoadPrivacyTweaks();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "Tweak error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }

    private static string GetStateText(
        TweakState state)
    {
        return state switch
        {
            TweakState.Applied =>
                "Disabled",

            TweakState.NotApplied =>
                "Enabled",

            TweakState.Partial =>
                "Partially configured",

            _ =>
                "Unknown"
        };
    }

    private static string GetButtonText(
        TweakState state)
    {
        return state switch
        {
            TweakState.Applied =>
                "Already disabled",

            TweakState.NotApplied =>
                "Disable",

            TweakState.Partial =>
                "Complete",

            _ =>
                "Unavailable"
        };
    }

    private static Brush GetStateColor(
        TweakState state)
    {
        return state switch
        {
            TweakState.Applied =>
                new SolidColorBrush(
                    Color.FromRgb(
                        34,
                        197,
                        94
                    )
                ),

            TweakState.NotApplied =>
                new SolidColorBrush(
                    Color.FromRgb(
                        245,
                        158,
                        11
                    )
                ),

            TweakState.Partial =>
                new SolidColorBrush(
                    Color.FromRgb(
                        245,
                        158,
                        11
                    )
                ),

            _ =>
                new SolidColorBrush(
                    Color.FromRgb(
                        148,
                        163,
                        184
                    )
                )
        };
    }
}