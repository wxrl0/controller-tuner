using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ControllerTuner.Models;

namespace ControllerTuner;

/// <summary>Slider/checkbox list bound (two-way) to the properties of its DataContext.</summary>
public class SettingsPanel : UserControl
{
    SettingsPanel((string Label, string Path, double Min, double Max)[] sliders,
                  (string Label, string Path)[] toggles)
    {
        var panel = new StackPanel();

        foreach (var (label, path, min, max) in sliders)
        {
            panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 10, 0, 0) });

            var value = new TextBlock { Width = 44, TextAlignment = TextAlignment.Right };
            var slider = new Slider { Minimum = min, Maximum = max };
            slider.SetBinding(Slider.ValueProperty, new Binding(path) { Mode = BindingMode.TwoWay });
            value.SetBinding(TextBlock.TextProperty, new Binding("Value") { Source = slider, StringFormat = "F2" });
            DockPanel.SetDock(value, Dock.Right);
            panel.Children.Add(new DockPanel { Children = { value, slider } });
        }

        foreach (var (label, path) in toggles)
        {
            var box = new CheckBox { Content = label, Margin = new Thickness(0, 12, 0, 0) };
            box.SetBinding(CheckBox.IsCheckedProperty, new Binding(path) { Mode = BindingMode.TwoWay });
            panel.Children.Add(box);
        }

        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    public static SettingsPanel ForStick(StickSettings s) => new(
        new[]
        {
            ("Inner dead zone", nameof(StickSettings.InnerDeadzone), 0.0, 0.6),
            ("Outer dead zone", nameof(StickSettings.OuterDeadzone), 0.0, 0.3),
            ("Anti dead zone (minimum output)", nameof(StickSettings.AntiDeadzone), 0.0, 0.5),
            ("Response curve (1 = linear)", nameof(StickSettings.Curve), 0.5, 3.0),
            ("Sensitivity", nameof(StickSettings.Sensitivity), 0.25, 2.0),
        },
        new[]
        {
            ("Invert X", nameof(StickSettings.InvertX)),
            ("Invert Y", nameof(StickSettings.InvertY)),
        }) { DataContext = s };

    public static SettingsPanel ForTriggers(Profile p) => new(
        new[]
        {
            ("Trigger dead zone (activation point)", nameof(Profile.TriggerDeadzone), 0.0, 0.5),
            ("Trigger max (full output at)", nameof(Profile.TriggerMax), 0.3, 1.0),
            ("Trigger sensitivity", nameof(Profile.TriggerSensitivity), 0.25, 2.0),
            ("Vibration strength (0 = off)", nameof(Profile.Vibration), 0.0, 1.5),
            ("Turbo speed (presses per second)", nameof(Profile.TurboRate), 4.0, 30.0),
        },
        new[]
        {
            ("Hair trigger (full press once past dead zone)", nameof(Profile.DigitalTriggers)),
            ("Swap left and right sticks", nameof(Profile.SwapSticks)),
        }) { DataContext = p };
}
