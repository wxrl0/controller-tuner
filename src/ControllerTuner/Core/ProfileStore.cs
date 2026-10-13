using System.IO;
using System.Text.Json;
using ControllerTuner.Models;

namespace ControllerTuner.Core;

public static class ProfileStore
{
    static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ControllerTuner");
    static readonly string FilePath = Path.Combine(Dir, "profiles.json");
    static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };

    public static List<Profile> Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var list = JsonSerializer.Deserialize<List<Profile>>(File.ReadAllText(FilePath));
                if (list is { Count: > 0 }) return list;
            }
        }
        catch { }
        return Defaults();
    }

    public static void Save(IEnumerable<Profile> profiles)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(profiles, Opts));
        }
        catch { }
    }

    public static Profile Clone(Profile p) =>
        JsonSerializer.Deserialize<Profile>(JsonSerializer.Serialize(p))!;

    static List<Profile> Defaults() => new()
    {
        new Profile { Name = "Default" },
        new Profile
        {
            Name = "Precise",
            LeftStick = new StickSettings { Curve = 1.6, Sensitivity = 0.8 },
            RightStick = new StickSettings { Curve = 1.6, Sensitivity = 0.7 },
        },
        new Profile
        {
            Name = "Fast",
            RightStick = new StickSettings { InnerDeadzone = 0.05, Sensitivity = 1.5 },
        },
    };
}
