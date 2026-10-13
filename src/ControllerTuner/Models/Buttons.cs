using System.Linq;

namespace ControllerTuner.Models;

public static class Buttons
{
    public static readonly (string Name, ushort Mask)[] All =
    {
        ("A", 0x1000), ("B", 0x2000), ("X", 0x4000), ("Y", 0x8000),
        ("LB", 0x0100), ("RB", 0x0200), ("L3", 0x0040), ("R3", 0x0080),
        ("Back", 0x0020), ("Start", 0x0010),
        ("D-Up", 0x0001), ("D-Down", 0x0002), ("D-Left", 0x0004), ("D-Right", 0x0008),
    };

    static readonly Dictionary<string, ushort> ByName = All.ToDictionary(b => b.Name, b => b.Mask);

    public static ushort Mask(string name) => ByName.TryGetValue(name, out var m) ? m : (ushort)0;
}
