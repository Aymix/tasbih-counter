using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using WinForms = System.Windows.Forms;

namespace TasbihCounter;

/// <summary>
/// Settings UI: per-dhikr label/color/enabled, HUD position (3x3 anchors),
/// HUD size, and how long the HUD stays. Edits a working copy of the config so
/// Cancel discards cleanly; Save raises <see cref="Saved"/> with the new config.
/// </summary>
public partial class SettingsWindow : Window
{
    /// <summary>Controls backing one dhikr row, so Save can read them back.</summary>
    private sealed class Row
    {
        public required DhikrConfig Config { get; init; }
        public required CheckBox Enabled { get; init; }
        public required TextBox Label { get; init; }
        public required Button Swatch { get; init; }
        public Color Color;
    }

    private readonly AppConfig _working;
    private readonly Action _resetCounts;
    private readonly List<Row> _rows = new();
    private readonly List<ToggleButton> _posButtons = new();
    private HudPosition _position;

    /// <summary>Raised when the user saves; carries the edited config.</summary>
    public event Action<AppConfig>? Saved;

    /// <param name="unavailableHotkeys">
    /// Combinations another app already owns, so the shortcut list can say they
    /// won't work rather than advertising a key that does nothing.
    /// </param>
    public SettingsWindow(AppConfig current, Action resetCounts,
                          IReadOnlyCollection<string>? unavailableHotkeys = null)
    {
        InitializeComponent();
        _working = Clone(current);
        _resetCounts = resetCounts;
        _position = _working.Position;

        BuildAdhkarRows();
        BuildPositionGrid();
        MarkUnavailableHotkeys(unavailableHotkeys);

        SizeSmall.IsChecked = _working.Size == HudSize.Small;
        SizeMedium.IsChecked = _working.Size == HudSize.Medium;
        SizeLarge.IsChecked = _working.Size == HudSize.Large;

        HoldSlider.Value = _working.HoldMs;
        HoldLabel.Text = $"{_working.HoldMs} ms";
        HoldSlider.ValueChanged += (_, e) => HoldLabel.Text = $"{(int)e.NewValue} ms";
    }

    private static AppConfig Clone(AppConfig c) => new()
    {
        Position = c.Position,
        Size = c.Size,
        HoldMs = c.HoldMs,
        Adhkar = c.Adhkar.Select(d => new DhikrConfig
        {
            Key = d.Key,
            Enabled = d.Enabled,
            Label = d.Label,
            ColorHex = d.ColorHex,
        }).ToList(),
    };

    /// <summary>Note any hotkey another app has claimed, so the list stays honest.</summary>
    private void MarkUnavailableHotkeys(IReadOnlyCollection<string>? unavailable)
    {
        if (unavailable is null || unavailable.Count == 0) return;

        var dimmed = new SolidColorBrush(Color.FromRgb(0x8A, 0x90, 0x96));

        if (unavailable.Contains("Ctrl+Alt+P"))
        {
            PauseDesc.Text = "Pause / resume — in use by another app; use the tray menu";
            PauseDesc.Foreground = dimmed;
        }
        if (unavailable.Contains("Ctrl+Alt+Q"))
        {
            QuitDesc.Text = "Quit — in use by another app; use the tray menu";
            QuitDesc.Foreground = dimmed;
        }
    }

    private void BuildAdhkarRows()
    {
        foreach (var dhikr in _working.Adhkar)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var enabled = new CheckBox
            {
                IsChecked = dhikr.Enabled,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                ToolTip = "Count this dhikr",
            };
            Grid.SetColumn(enabled, 0);

            var key = new TextBlock
            {
                Text = dhikr.Key.ToString(),
                Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0xA0, 0xA6)),
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = $"Tap {dhikr.Key} to count this dhikr",
            };
            Grid.SetColumn(key, 1);

            var label = new TextBox
            {
                Text = dhikr.Label,
                FlowDirection = FlowDirection.RightToLeft,
                FontSize = 15,
                Margin = new Thickness(0, 0, 8, 0),
            };
            Grid.SetColumn(label, 2);

            var color = ConfigStore.ParseColor(dhikr.ColorHex);
            var swatch = new Button
            {
                Width = 34,
                Height = 26,
                Background = new SolidColorBrush(color),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3C, 0x40, 0x43)),
                ToolTip = "Pick a color",
            };
            Grid.SetColumn(swatch, 3);

            var row = new Row
            {
                Config = dhikr,
                Enabled = enabled,
                Label = label,
                Swatch = swatch,
                Color = color,
            };
            swatch.Click += (_, _) => PickColor(row);

            grid.Children.Add(enabled);
            grid.Children.Add(key);
            grid.Children.Add(label);
            grid.Children.Add(swatch);
            AdhkarPanel.Children.Add(grid);
            _rows.Add(row);
        }
    }

    private void PickColor(Row row)
    {
        using var dlg = new WinForms.ColorDialog
        {
            FullOpen = true,
            Color = System.Drawing.Color.FromArgb(row.Color.R, row.Color.G, row.Color.B),
        };
        if (dlg.ShowDialog() != WinForms.DialogResult.OK) return;

        row.Color = Color.FromRgb(dlg.Color.R, dlg.Color.G, dlg.Color.B);
        row.Swatch.Background = new SolidColorBrush(row.Color);
    }

    private void BuildPositionGrid()
    {
        // Enum order matches the 3x3 reading order: TopLeft .. BottomRight.
        for (int i = 0; i < 9; i++)
        {
            var pos = (HudPosition)i;
            var btn = new ToggleButton
            {
                Margin = new Thickness(3),
                ToolTip = pos.ToString(),
                Content = "",
            };
            btn.Checked += (_, _) => SelectPosition(pos);
            btn.Click += (_, _) => SelectPosition(pos); // re-clicking the active one keeps it
            _posButtons.Add(btn);
            PositionGrid.Children.Add(btn);
        }
        RefreshPositionButtons();
    }

    private void SelectPosition(HudPosition pos)
    {
        _position = pos;
        RefreshPositionButtons();
    }

    private void RefreshPositionButtons()
    {
        var on = new SolidColorBrush(Color.FromRgb(0x66, 0xE0, 0xA3));
        var off = new SolidColorBrush(Color.FromRgb(0x2A, 0x2B, 0x2D));
        var edge = new SolidColorBrush(Color.FromRgb(0x3C, 0x40, 0x43));

        for (int i = 0; i < _posButtons.Count; i++)
        {
            bool active = (HudPosition)i == _position;
            _posButtons[i].IsChecked = active;
            _posButtons[i].Background = active ? on : off;
            _posButtons[i].BorderBrush = edge;
        }
    }

    private HudSize SelectedSize()
    {
        if (SizeSmall.IsChecked == true) return HudSize.Small;
        if (SizeLarge.IsChecked == true) return HudSize.Large;
        return HudSize.Medium;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        foreach (var row in _rows)
        {
            row.Config.Enabled = row.Enabled.IsChecked == true;
            row.Config.Label = row.Label.Text.Trim();
            row.Config.ColorHex = ConfigStore.ToHex(row.Color);
        }

        _working.Position = _position;
        _working.Size = SelectedSize();
        _working.HoldMs = (int)HoldSlider.Value;

        Saved?.Invoke(_working);
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();

    private void OnResetCounts(object sender, RoutedEventArgs e) => _resetCounts();
}
