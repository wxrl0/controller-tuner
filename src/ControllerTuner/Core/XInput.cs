using System.Runtime.InteropServices;

namespace ControllerTuner.Core;

static class XInput
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Gamepad
    {
        public ushort Buttons;
        public byte LeftTrigger, RightTrigger;
        public short ThumbLX, ThumbLY, ThumbRX, ThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct State
    {
        public uint PacketNumber;
        public Gamepad Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct Vibration
    {
        public ushort Left, Right;
    }

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    static extern uint GetState(uint index, out State state);

    [DllImport("xinput1_4.dll", EntryPoint = "XInputSetState")]
    static extern uint SetState(uint index, ref Vibration vibration);

    public static bool TryGet(int index, out uint packet, out Gamepad pad)
    {
        bool ok = GetState((uint)index, out var s) == 0;
        packet = s.PacketNumber;
        pad = s.Gamepad;
        return ok;
    }

    public static void SetVibration(int index, ushort left, ushort right)
    {
        var v = new Vibration { Left = left, Right = right };
        SetState((uint)index, ref v);
    }
}
