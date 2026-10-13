using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ControllerTuner.Core;
using ControllerTuner.Models;

namespace ControllerTuner;

public partial class MainWindow : Window
{
    readonly Engine _engine = new();
    readonly ObservableCollection<Profile> _profiles;
    readonly DispatcherTimer _ui = new() { Interval = TimeSpan.FromMilliseconds(100) };
    string? _lastForeground;
    int _tick;

    public MainWindow()
    {
        InitializeComponent();

        _profiles = new ObservableCollection<Profile>(ProfileStore.Load());
        ProfileBox.ItemsSource = _profiles;
        ProfileBox.SelectedIndex = 0;

        _engine.SwitchRequested += dir => Dispatcher.InvokeAsync(() => CycleProfile(dir));
        _engine.Start();
        _ui.Tick += (_, _) => Refresh();
        _ui.Start();

        Closing += (_, _) =>
        {
            ProfileStore.Save(_profiles);
            _engine.Dispose();
        };
    }

    void ProfileBox_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (ProfileBox.SelectedItem is not Profile p) return;
        _engine.Profile = p;
        ProfileHeader.DataContext = p;
        LeftHost.Content = SettingsPanel.ForStick(p.LeftStick);
        RightHost.Content = SettingsPanel.ForStick(p.RightStick);
        TrigHost.Content = SettingsPanel.ForTriggers(p);
        ButtonsHost.Content = new ButtonsPanel(p);
        MacrosHost.Content = new MacrosPanel(p, _engine);
    }

    void CycleProfile(int dir)
    {
        int n = _profiles.Count;
        if (n < 2) return;
        ProfileBox.SelectedIndex = ((ProfileBox.SelectedIndex + dir) % n + n) % n;
    }

    void Duplicate_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileBox.SelectedItem is not Profile p) return;
        var copy = ProfileStore.Clone(p);
        copy.Name = p.Name + " copy";
        _profiles.Add(copy);
        ProfileBox.SelectedItem = copy;
    }

    void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_profiles.Count < 2 || ProfileBox.SelectedItem is not Profile p) return;
        _profiles.Remove(p);
        ProfileBox.SelectedIndex = 0;
    }

    void Bypass_Click(object sender, RoutedEventArgs e) => _engine.Bypass = BypassBox.IsChecked == true;

    void Hotkey_Click(object sender, RoutedEventArgs e) => _engine.HotkeysEnabled = HotkeyBox.IsChecked == true;

    static string Norm(string? s) => (s ?? "").Trim().ToLowerInvariant().Replace(".exe", "");

    /// <summary>When the focused app changes, select the profile whose game exe matches it.</summary>
    void AutoSwitch()
    {
        var fg = Foreground.ProcessName();
        if (fg == null || fg == _lastForeground) return;
        _lastForeground = fg;
        string key = fg.ToLowerInvariant();
        var match = _profiles.FirstOrDefault(p => Norm(p.GameExe).Length > 0 && Norm(p.GameExe) == key);
        if (match != null && !ReferenceEquals(match, ProfileBox.SelectedItem)) ProfileBox.SelectedItem = match;
    }

    void Refresh()
    {
        Dot.Fill = _engine.BusOk ? Brushes.LimeGreen : Brushes.OrangeRed;
        StatusText.Text = _engine.BusStatus;
        PadText.Text = _engine.PhysicalSlot >= 0
            ? $"Physical controller: XInput slot {_engine.PhysicalSlot + 1}"
            : "No physical controller found";
        Live.Text = $"L stick  in ({_engine.InX:F2}, {_engine.InY:F2})  ->  out ({_engine.OutX:F2}, {_engine.OutY:F2})";

        if (++_tick % 10 == 0) AutoSwitch();
    }
}
