using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ControllerTuner.Core;

static class Foreground
{
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

    /// <summary>Process name of the focused window (no ".exe"), or null.</summary>
    public static string? ProcessName()
    {
        try
        {
            var h = GetForegroundWindow();
            if (h == IntPtr.Zero) return null;
            GetWindowThreadProcessId(h, out var pid);
            using var proc = Process.GetProcessById((int)pid);
            return proc.ProcessName;
        }
        catch { return null; }
    }
}
