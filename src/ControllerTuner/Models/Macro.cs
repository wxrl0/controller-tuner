using System.ComponentModel;

namespace ControllerTuner.Models;

public class MacroStep
{
    public List<string> Press { get; set; } = new(); // buttons held during this step (empty = pure wait)
    public int HoldMs { get; set; } = 50;
    public int WaitMs { get; set; } = 50;            // release gap before the next step
}

public class Macro : INotifyPropertyChanged
{
    string _name = "Macro";

    public string Name
    {
        get => _name;
        set { _name = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name))); }
    }

    /// <summary>Physical button that starts the macro (hidden from the game while the macro exists).</summary>
    public string Trigger { get; set; } = "D-Left";

    /// <summary>"Once" = play on press, "Hold" = repeat while held, "Toggle" = repeat until pressed again.</summary>
    public string Mode { get; set; } = "Hold";

    // Replaced (never mutated) so the input thread can read it safely.
    public List<MacroStep> Steps { get; set; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
}
