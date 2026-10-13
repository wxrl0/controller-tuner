using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ControllerTuner.Core;
using ControllerTuner.Models;

namespace ControllerTuner;

/// <summary>Create, edit and record button macros for a profile.</summary>
public class MacrosPanel : UserControl
{
    const string RecordLabel = "Record from controller";

    readonly Profile _p;
    readonly Engine _engine;
    readonly ObservableCollection<Macro> _items;

    readonly ListBox _list = new() { Height = 90, DisplayMemberPath = "Name" };
    readonly TextBox _name = new();
    readonly ComboBox _trigger = new() { ItemsSource = Buttons.All.Select(b => b.Name).ToList(), Width = 110 };
    readonly ComboBox _mode = new() { ItemsSource = new[] { "Once", "Hold", "Toggle" }, Width = 110 };
    readonly TextBox _steps = new()
    {
        AcceptsReturn = true,
        FontFamily = new FontFamily("Consolas"),
        MinHeight = 120,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
    };
    readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
    readonly Button _record = new()
    {
        Content = RecordLabel,
        Padding = new Thickness(8, 2, 8, 2),
        Margin = new Thickness(0, 8, 0, 0),
        HorizontalAlignment = HorizontalAlignment.Left,
    };
    readonly StackPanel _editor = new() { IsEnabled = false };

    Macro? _sel;
    bool _loading, _recording;

    public MacrosPanel(Profile p, Engine engine)
    {
        _p = p;
        _engine = engine;
        _items = new ObservableCollection<Macro>(p.Macros);
        _list.ItemsSource = _items;

        var add = new Button { Content = "Add", Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 4, 0) };
        var del = new Button { Content = "Delete", Padding = new Thickness(8, 2, 8, 2) };
        add.Click += (_, _) => AddMacro();
        del.Click += (_, _) => DeleteMacro();

        _list.SelectionChanged += (_, _) =>
        {
            _sel = _list.SelectedItem as Macro;
            LoadSelected();
        };
        _name.TextChanged += (_, _) =>
        {
            if (!_loading && _sel != null) _sel.Name = _name.Text;
        };
        _trigger.SelectionChanged += (_, _) =>
        {
            if (!_loading && _sel != null && _trigger.SelectedItem is string t) _sel.Trigger = t;
        };
        _mode.SelectionChanged += (_, _) =>
        {
            if (!_loading && _sel != null && _mode.SelectedItem is string m) _sel.Mode = m;
        };
        _steps.TextChanged += (_, _) => OnStepsEdited();
        _record.Click += (_, _) => ToggleRecord();
        Unloaded += (_, _) => StopRecordingQuietly();

        var listButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 4, 0, 8),
            Children = { add, del },
        };
        var pickRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 8, 0, 0),
            Children =
            {
                new TextBlock { Text = "Trigger", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) },
                _trigger,
                new TextBlock { Text = "Mode", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 6, 0) },
                _mode,
            },
        };

        _editor.Children.Add(new TextBlock { Text = "Name" });
        _editor.Children.Add(_name);
        _editor.Children.Add(pickRow);
        _editor.Children.Add(new TextBlock
        {
            Text = "Once = play on press.  Hold = repeat while held.  Toggle = repeat until pressed again.  "
                 + "The trigger button is hidden from the game. Macros are off while Bypass is on.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Gray,
            Margin = new Thickness(0, 6, 0, 8),
        });
        _editor.Children.Add(new TextBlock { Text = "Steps (one per line):  BUTTON[+BUTTON] holdMs waitMs" });
        _editor.Children.Add(_steps);
        _editor.Children.Add(new TextBlock
        {
            Text = "Use - for a pure wait. Buttons: A B X Y LB RB L3 R3 Back Start D-Up D-Down D-Left D-Right",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Gray,
            Margin = new Thickness(0, 4, 0, 0),
        });
        _editor.Children.Add(_record);
        _editor.Children.Add(_status);

        Content = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new StackPanel { Children = { _list, listButtons, _editor } },
        };
    }

    void Sync() => _p.Macros = _items.ToList();

    void AddMacro()
    {
        var m = new Macro
        {
            Name = "Macro " + (_items.Count + 1),
            Steps = new List<MacroStep> { new MacroStep { Press = new List<string> { "A" } } },
        };
        _items.Add(m);
        Sync();
        _list.SelectedItem = m;
    }

    void DeleteMacro()
    {
        if (_sel == null) return;
        _items.Remove(_sel);
        Sync();
        _list.SelectedIndex = _items.Count > 0 ? 0 : -1;
    }

    void LoadSelected()
    {
        _loading = true;
        _editor.IsEnabled = _sel != null;
        if (_sel != null)
        {
            _name.Text = _sel.Name;
            _trigger.SelectedItem = _sel.Trigger;
            _mode.SelectedItem = _sel.Mode;
            _steps.Text = StepsToText(_sel.Steps);
        }
        else
        {
            _name.Text = "";
            _steps.Text = "";
        }
        _status.Text = "";
        _loading = false;
    }

    void OnStepsEdited()
    {
        if (_loading || _sel == null) return;
        if (TryParse(_steps.Text, out var steps, out var error))
        {
            _sel.Steps = steps;
            _status.Text = steps.Count + " step(s) saved";
            _status.Foreground = Brushes.Gray;
        }
        else
        {
            _status.Text = error + " (not applied)";
            _status.Foreground = Brushes.OrangeRed;
        }
    }

    void ToggleRecord()
    {
        if (!_recording)
        {
            if (_sel == null)
            {
                _status.Text = "Add or select a macro first.";
                _status.Foreground = Brushes.OrangeRed;
                return;
            }
            _engine.RecordStart();
            _recording = true;
            _record.Content = "Stop recording";
            _status.Text = "Recording... press buttons on your controller, then click Stop.";
            _status.Foreground = Brushes.Gray;
            return;
        }

        _recording = false;
        _record.Content = RecordLabel;
        var steps = _engine.RecordStop();
        if (steps.Count == 0 || _sel == null)
        {
            _status.Text = "Nothing recorded.";
            return;
        }

        _sel.Steps = steps;
        _loading = true;
        _steps.Text = StepsToText(steps);
        _loading = false;
        _status.Text = "Recorded " + steps.Count + " step(s). Edit the timings above if you like.";
        _status.Foreground = Brushes.Gray;
    }

    void StopRecordingQuietly()
    {
        if (!_recording) return;
        _recording = false;
        _engine.RecordStop();
        _record.Content = RecordLabel;
    }

    static string StepsToText(List<MacroStep> steps) =>
        string.Join(Environment.NewLine, steps.Select(s =>
            (s.Press.Count == 0 ? "-" : string.Join("+", s.Press)) + " " + s.HoldMs + " " + s.WaitMs));

    static bool TryParse(string text, out List<MacroStep> steps, out string error)
    {
        steps = new List<MacroStep>();
        error = "";
        int lineNo = 0;

        foreach (var rawLine in text.Split('\n'))
        {
            lineNo++;
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var press = new List<string>();

            if (parts[0] != "-")
            {
                foreach (var n in parts[0].Split('+', StringSplitOptions.RemoveEmptyEntries))
                {
                    var match = Buttons.All.FirstOrDefault(b => string.Equals(b.Name, n, StringComparison.OrdinalIgnoreCase));
                    if (match.Name == null)
                    {
                        error = "Line " + lineNo + ": unknown button '" + n + "'";
                        return false;
                    }
                    press.Add(match.Name);
                }
            }

            int hold = 50, wait = 50;
            if (parts.Length > 1 && !int.TryParse(parts[1], out hold))
            {
                error = "Line " + lineNo + ": hold time must be a number";
                return false;
            }
            if (parts.Length > 2 && !int.TryParse(parts[2], out wait))
            {
                error = "Line " + lineNo + ": wait time must be a number";
                return false;
            }

            steps.Add(new MacroStep
            {
                Press = press,
                HoldMs = Math.Clamp(hold, 1, 60000),
                WaitMs = Math.Clamp(wait, 0, 60000),
            });
        }
        return true;
    }
}
