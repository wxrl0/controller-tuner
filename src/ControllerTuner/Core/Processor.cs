using ControllerTuner.Models;

namespace ControllerTuner.Core;

public static class Processor
{
    /// <summary>x/y in -1..1. Radial dead zone, anti-dead zone, curve, sensitivity.</summary>
    public static (double X, double Y) Stick(double x, double y, StickSettings s)
    {
        if (s.InvertX) x = -x;
        if (s.InvertY) y = -y;

        double mag = Math.Sqrt(x * x + y * y);
        if (mag < 1e-6 || mag <= s.InnerDeadzone) return (0, 0);

        double span = Math.Max(1 - s.OuterDeadzone - s.InnerDeadzone, 0.01);
        double n = Math.Clamp((mag - s.InnerDeadzone) / span, 0, 1);
        n = Math.Pow(n, s.Curve);
        n = s.AntiDeadzone + n * (1 - s.AntiDeadzone);
        n = Math.Min(n * s.Sensitivity, 1);

        return (x / mag * n, y / mag * n);
    }

    public static byte Trigger(byte raw, double deadzone, double max, double sensitivity, bool digital)
    {
        double v = raw / 255.0;
        if (v <= deadzone) return 0;
        if (digital) return 255;
        double span = Math.Max(max - deadzone, 0.01);
        double n = Math.Min((v - deadzone) / span * sensitivity, 1);
        return (byte)Math.Round(n * 255);
    }
}
