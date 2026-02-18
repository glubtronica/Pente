using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;

namespace MultiPente;

/// <summary>
/// Drop-in menu page to configure a match:
/// - 2–4 players
/// - custom player names
/// - toggles like Animations on/off for perf testing
/// </summary>
public sealed class GameMenuPage : ContentPage
{
    private readonly Picker _playerCountPicker;
    private readonly Switch _animationsSwitch;
    private readonly Switch _shadowsSwitch;
    private readonly Switch _lastMoveSwitch;

    private readonly Grid _namesGrid;
    private readonly List<Entry> _nameEntries = new();

    // Tokens fixed to match your current engine order
    private static readonly char[] Tokens = { 'G', 'B', 'W', 'K' };

    public GameMenuPage()
    {
        Title = "Multi-Pente";

        BackgroundColor = Color.FromArgb("#0F1115");

        var title = new Label
        {
            Text = "MULTI-PENTE",
            FontSize = 26,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#E5E7EB")
        };

        var subtitle = new Label
        {
            Text = "Mixed-color pair capture • Tap to place • Pinch to zoom",
            FontSize = 13,
            TextColor = Color.FromArgb("#9CA3AF")
        };

        _playerCountPicker = new Picker
        {
            Title = "Players",
            TextColor = Color.FromArgb("#E5E7EB"),
            BackgroundColor = Color.FromArgb("#1A1E24")
        };
        _playerCountPicker.ItemsSource = new[] { "2", "3", "4" };
        _playerCountPicker.SelectedIndex = 2; // default 4 players
        _playerCountPicker.SelectedIndexChanged += (_, __) => RebuildNameInputs();

        _animationsSwitch = MakeSwitch(true);
        _shadowsSwitch = MakeSwitch(true);
        _lastMoveSwitch = MakeSwitch(true);

        _namesGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            RowSpacing = 10,
            ColumnSpacing = 12
        };

        var startButton = new Button
        {
            Text = "Start Game",
            FontSize = 16,
            CornerRadius = 14,
            BackgroundColor = Color.FromArgb("#2563EB"),
            TextColor = Colors.White,
            Padding = new Thickness(14, 12)
        };
        startButton.Clicked += Start_Clicked;

        var perfHint = new Label
        {
            Text = "Tip: Toggle Animations off for quick performance testing on older phones.",
            FontSize = 12,
            TextColor = Color.FromArgb("#9CA3AF")
        };

        var card = new Border
        {
            BackgroundColor = Color.FromArgb("#1A1E24"),
            Stroke = Color.FromArgb("#2B313A"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Padding = 14,
            Content = new VerticalStackLayout
            {
                Spacing = 14,
                Children =
                {
                    new Label { Text = "Match Setup", FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#E5E7EB") },

                    Labeled("Player count", _playerCountPicker),

                    new Label { Text = "Player names", FontSize = 13, TextColor = Color.FromArgb("#9CA3AF") },
                    _namesGrid,

                    new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#2B313A"), Opacity = 0.9 },

                    new Label { Text = "Performance toggles", FontSize = 13, TextColor = Color.FromArgb("#9CA3AF") },
                    ToggleRow("Animations", _animationsSwitch, "Stone/capture UI effects (when implemented)"),
                    ToggleRow("Shadows", _shadowsSwitch, "Draw stone shadows (slightly heavier)"),
                    ToggleRow("Last move ring", _lastMoveSwitch, "Highlight the most recent move"),

                    perfHint,
                    startButton
                }
            }
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 16,
                Spacing = 12,
                Children = { title, subtitle, card }
            }
        };

        RebuildNameInputs();
    }

    private static Switch MakeSwitch(bool on) => new Switch
    {
        IsToggled = on,
        OnColor = Color.FromArgb("#34D399")
    };

    private static View Labeled(string label, View control)
    {
        return new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                new Label { Text = label, FontSize = 12, TextColor = Color.FromArgb("#9CA3AF") },
                control
            }
        };
    }

    private static View ToggleRow(string title, Switch sw, string desc)
    {
        return new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            Padding = new Thickness(2, 0),
            Children =
            {
                new Label { Text = title, FontSize = 14, TextColor = Color.FromArgb("#E5E7EB") }.Row(0).Column(0),
                new Label { Text = desc, FontSize = 12, TextColor = Color.FromArgb("#9CA3AF") }.Row(1).Column(0),
                sw.RowSpan(2).Column(1).VerticalOptions(LayoutOptions.Center)
            }
        };
    }

    private int PlayerCount => int.Parse((string)_playerCountPicker.SelectedItem);

    private void RebuildNameInputs()
    {
        _namesGrid.RowDefinitions.Clear();
        _namesGrid.Children.Clear();
        _nameEntries.Clear();

        int n = PlayerCount;

        for (int i = 0; i < n; i++)
        {
            _namesGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            var tokenChip = new Border
            {
                BackgroundColor = TokenColor(Tokens[i]),
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 10 },
                Padding = new Thickness(10, 6),
                Content = new Label
                {
                    Text = Tokens[i].ToString(),
                    FontAttributes = FontAttributes.Bold,
                    TextColor = TokenTextColor(Tokens[i]),
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            };

            var entry = new Entry
            {
                Placeholder = $"Player {i + 1}",
                Text = $"P{i + 1}",
                TextColor = Color.FromArgb("#E5E7EB"),
                PlaceholderColor = Color.FromArgb("#6B7280"),
                BackgroundColor = Color.FromArgb("#111318"),
                ClearButtonVisibility = ClearButtonVisibility.WhileEditing
            };

            _namesGrid.Add(tokenChip, 0, i);
            _namesGrid.Add(entry, 1, i);

            _nameEntries.Add(entry);
        }
    }

    private async void Start_Clicked(object? sender, EventArgs e)
    {
        // Build settings + players
        var players = new List<GameSetupPlayer>();
        for (int i = 0; i < PlayerCount; i++)
        {
            string name = (_nameEntries[i].Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(name)) name = $"P{i + 1}";
            players.Add(new GameSetupPlayer(name, Tokens[i]));
        }

        var settings = new GameUiSettings(
            AnimationsEnabled: _animationsSwitch.IsToggled,
            ShadowsEnabled: _shadowsSwitch.IsToggled,
            LastMoveHighlightEnabled: _lastMoveSwitch.IsToggled
        );

        // IMPORTANT: requires the small MainPage constructor hook shown below.
        var gamePage = new MainPage(players, settings);

        await Navigation.PushAsync(gamePage);
    }

    // --- token colors ---
    private static Color TokenColor(char t) => t switch
    {
        'G' => Color.FromArgb("#34D399"),
        'B' => Color.FromArgb("#60A5FA"),
        'W' => Color.FromArgb("#F9FAFB"),
        'K' => Color.FromArgb("#111827"),
        _ => Colors.Magenta
    };

    private static Color TokenTextColor(char t) => t switch
    {
        'W' => Color.FromArgb("#111827"),
        _ => Colors.White
    };
}

/// <summary>Simple DTO for menu → game.</summary>
public sealed record GameSetupPlayer(string Name, char Token);

/// <summary>
/// UI/perf toggles. Hook these into drawing/animations.
/// </summary>
public sealed record GameUiSettings(
    bool AnimationsEnabled,
    bool ShadowsEnabled,
    bool LastMoveHighlightEnabled
);

/// <summary>
/// Tiny helper extensions for fluent Grid placement.
/// </summary>
internal static class ViewGridExt
{
    public static T Row<T>(this T v, int row) where T : View { Grid.SetRow(v, row); return v; }
    public static T Column<T>(this T v, int col) where T : View { Grid.SetColumn(v, col); return v; }
    public static T RowSpan<T>(this T v, int span) where T : View { Grid.SetRowSpan(v, span); return v; }
}