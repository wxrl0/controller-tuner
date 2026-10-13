using System.ComponentModel;

namespace ControllerTuner.Models;

public class StickSettings
{
    public double InnerDeadzone { get; set; } = 0.10; // ignore input below this radius
    public double OuterDeadzone { get; set; } = 0.02; // reach full output this far from the rim
    public double AntiDeadzone { get; set; } = 0.0;   // minimum output once outside the inner dead zone
    public double Curve { get; set; } = 1.0;          // 1 = linear, >1 = finer control near center
    public double Sensitivity { get; set; } = 1.0;    // output multiplier
    public bool InvertX { get; set; }
    public bool InvertY { get; set; }
}

public class Profile : INotifyPropertyChanged
{
    string _name = "Default";

    public string Name
    {
        get => _name;
        set { _name = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name))); }
    }

    /// <summary>Process name (e.g. "cod" or "cod.exe") that auto-selects this profile when focused.</summary>
    public string GameExe { get; set; } = "";

    public StickSettings LeftStick { get; set; } = new();
    public StickSettings RightStick { get; set; } = new();

    public double TriggerDeadzone { get; set; } = 0.05;   // activation point
    public double TriggerMax { get; set; } = 1.0;         // full output reached here (trigger stop)
    public double TriggerSensitivity { get; set; } = 1.0;
    public bool DigitalTriggers { get; set; }             // hair trigger

    public double Vibration { get; set; } = 1.0;          // rumble strength, 0 = off
    public double TurboRate { get; set; } = 12;           // presses per second
    public bool SwapSticks { get; set; }

    // Replaced (never mutated) so the input thread can read them safely.
    public Dictionary<string, string> ButtonMap { get; set; } = new();
    public List<string> TurboButtons { get; set; } = new();
    public List<Macro> Macros { get; set; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
}
