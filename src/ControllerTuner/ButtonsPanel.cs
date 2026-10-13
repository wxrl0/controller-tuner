using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ControllerTuner.Models;

namespace ControllerTuner;

/// <summary>Per-button remap (or disable) and turbo toggle.</summary>
public class ButtonsPanel : UserControl
{
    public ButtonsPanel(Profile p)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 8, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var targets = new[] { "None" }.Concat(Buttons.All.Select(b => b.Name)).ToList();
        int row = 0;

        foreach (var (name, _) in Buttons.All)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center };
            var combo = new ComboBox { ItemsSource = targets, Margin = new Thickness(0, 3, 0, 3) };
            combo.SelectedItem = p.ButtonMap.TryGetValue(name, out var mapped) ? mapped : name;
            var turbo = new CheckBox
            {
                Content = "Turbo",
                IsChecked = p.TurboButtons.Contains(name),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0),
            };

            combo.SelectionChanged += (_, _) =>
            {
                var map = new Dictionary<string, string>(p.ButtonMap);
                if (combo.SelectedItem is string target && target != name) map[name] = target;
                else map.Remove(name);
                p.ButtonMap = map;
            };
            turbo.Click += (_, _) =>
            {
                var list = new List<string>(p.TurboButtons);
                list.Remove(name);
                if (turbo.IsChecked == true) list.Add(name);
                p.TurboButtons = list;
            };

            Grid.SetRow(label, row); Grid.SetColumn(label, 0);
            Grid.SetRow(combo, row); Grid.SetColumn(combo, 1);
            Grid.SetRow(turbo, row); Grid.SetColumn(turbo, 2);
            grid.Children.Add(label);
            grid.Children.Add(combo);
            grid.Children.Add(turbo);
            row++;
        }

        var reset = new Button
        {
            Content = "Reset remaps and turbo",
            Margin = new Thickness(0, 10, 0, 0),
            Padding = new Thickness(8, 2, 8, 2),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        reset.Click += (_, _) =>
        {
            p.ButtonMap = new Dictionary<string, string>();
            p.TurboButtons = new List<string>();
            Content = new ButtonsPanel(p);
        };

        Content = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new StackPanel { Children = { grid, reset } },
        };
    }
}
