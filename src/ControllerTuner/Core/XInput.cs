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

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    static extern uint GetState(uint index, out State state);

    public static bool TryGet(int index, out uint packet, out Gamepad pad)
    {
        bool ok = GetState((uint)index, out var s) == 0;
        packet = s.PacketNumber;
        pad = s.Gamepad;
        return ok;
    }
}
