using System.Diagnostics;
using System.Runtime.InteropServices;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using ControllerTuner.Models;

namespace ControllerTuner.Core;

/// <summary>Reads the physical XInput pad, tunes it, and forwards it to a ViGEm virtual Xbox 360 pad.
/// Rumble from the game is forwarded back to the physical pad.</summary>
public sealed class Engine : IDisposable
{
    [DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint ms);
    [DllImport("winmm.dll")] static extern uint timeEndPeriod(uint ms);

    const ushort Back = 0x0020, LB = 0x0100, RB = 0x0200;

    ViGEmClient? _client;
    IXbox360Controller? _pad;
    Thread? _thread;
    volatile bool _run;
    int _virtualSlot = -1;
    uint _lastPacket;
    bool _wasActive, _hotkeyHeld;
    long _lastPhase = -1;
    byte _large, _small;
    readonly Stopwatch _clock = Stopwatch.StartNew();

    readonly MacroRunner _macros = new();
    readonly List<(long T, ushort Mask)> _rec = new();
    volatile bool _recording;
    ushort _recMask;

    bool _haveReport;
    (ushort, short, short, short, short, byte, byte) _lastReport;

    public Profile Profile { get; set; } = new();
    public volatile bool Bypass;
    public volatile bool HotkeysEnabled = true;
    public string BusStatus { get; private set; } = "Starting...";
    public bool BusOk => _pad != null;
    public int PhysicalSlot { get; private set; } = -1;
    public int VirtualSlot => _virtualSlot;

    /// <summary>Raised (on the input thread) when Back+RB (+1) or Back+LB (-1) is pressed.</summary>
    public event Action<int>? SwitchRequested;

    // Left stick live values for the UI preview
    public double InX, InY, OutX, OutY;

    public void Start()
    {
        timeBeginPeriod(1);
        _run = true;
        _thread = new Thread(Run) { IsBackground = true, Name = "pad-loop" };
        _thread.Start();
    }

    // ---- macro recording (button presses and timing from the physical pad) ----

    public void RecordStart()
    {
        lock (_rec)
        {
            _rec.Clear();
            _recMask = 0;
            _rec.Add((_clock.ElapsedMilliseconds, (ushort)0));
            _recording = true;
        }
    }

    public List<MacroStep> RecordStop()
    {
        List<(long T, ushort Mask)> events;
        lock (_rec)
        {
            _recording = false;
            _rec.Add((_clock.ElapsedMilliseconds, (ushort)0));
            events = new List<(long T, ushort Mask)>(_rec);
        }
        return Recorder.ToSteps(events);
    }

    void Record(ushort mask, long now)
    {
        lock (_rec)
        {
            if (!_recording || mask == _recMask) return;
            _recMask = mask;
            _rec.Add((now, mask));
        }
    }

    // ---- main loop ----

    void Run()
    {
        var scan = Stopwatch.StartNew();
        while (_run)
        {
            var pad = _pad;
            if (pad == null)
            {
                TryConnectBus();
                if (_pad == null) Thread.Sleep(1000);
                continue;
            }

            XInput.Gamepad gp = default;
            uint packet = 0;
            bool have = PhysicalSlot >= 0 && XInput.TryGet(PhysicalSlot, out packet, out gp);
            if (!have)
            {
                if (_wasActive)
                {
                    pad.ResetReport();
                    pad.SubmitReport();
                    _wasActive = false;
                    _haveReport = false;
                    _macros.Reset();
                }
                PhysicalSlot = -1;
                if (scan.ElapsedMilliseconds >= 500)
                {
                    scan.Restart();
                    PhysicalSlot = FindPhysical();
                }
                Thread.Sleep(20);
                continue;
            }

            var p = Profile;
            long now = _clock.ElapsedMilliseconds;
            long phase = (long)(now / 1000.0 * Math.Max(p.TurboRate, 1) * 2);
            bool turboTick = p.TurboButtons.Count > 0 && phase != _lastPhase;
            bool changed = packet != _lastPacket || !_wasActive;

            if (_recording && changed) Record(gp.Buttons, now);

            if (changed || turboTick || _macros.Active)
            {
                _lastPacket = packet;
                _lastPhase = phase;
                _wasActive = true;
                Send(pad, gp, (phase & 1) == 0);
            }
            Thread.Sleep(1);
        }
    }

    void Send(IXbox360Controller pad, XInput.Gamepad g, bool turboOn)
    {
        var p = Profile;
        ushort buttons = g.Buttons;

        // Back + LB/RB switches profile and is hidden from the game.
        bool combo = HotkeysEnabled && (buttons & Back) != 0 && (buttons & (LB | RB)) != 0;
        if (combo)
        {
            if (!_hotkeyHeld)
            {
                _hotkeyHeld = true;
                SwitchRequested?.Invoke((buttons & RB) != 0 ? 1 : -1);
            }
            buttons = (ushort)(buttons & ~(Back | LB | RB));
        }
        else
        {
            _hotkeyHeld = false;
        }

        double lx = Norm(g.ThumbLX), ly = Norm(g.ThumbLY), rx = Norm(g.ThumbRX), ry = Norm(g.ThumbRY);
        byte lt = g.LeftTrigger, rt = g.RightTrigger;
        InX = lx; InY = ly;

        if (!Bypass)
        {
            var (macroAdd, macroHide) = _macros.Update(p, buttons, _clock.ElapsedMilliseconds);
            buttons = Remap((ushort)(buttons & ~macroHide), p, turboOn);
            buttons |= macroAdd;

            (lx, ly) = Processor.Stick(lx, ly, p.LeftStick);
            (rx, ry) = Processor.Stick(rx, ry, p.RightStick);
            lt = Processor.Trigger(lt, p.TriggerDeadzone, p.TriggerMax, p.TriggerSensitivity, p.DigitalTriggers);
            rt = Processor.Trigger(rt, p.TriggerDeadzone, p.TriggerMax, p.TriggerSensitivity, p.DigitalTriggers);
            if (p.SwapSticks) (lx, ly, rx, ry) = (rx, ry, lx, ly);
        }
        else
        {
            _macros.Reset();
        }
        OutX = lx; OutY = ly;

        // Only submit when something actually changed.
        var report = (buttons, ToShort(lx), ToShort(ly), ToShort(rx), ToShort(ry), lt, rt);
        if (_haveReport && report == _lastReport) return;
        _lastReport = report;
        _haveReport = true;

        pad.SetButtonsFull(buttons);
        pad.SetAxisValue(Xbox360Axis.LeftThumbX, report.Item2);
        pad.SetAxisValue(Xbox360Axis.LeftThumbY, report.Item3);
        pad.SetAxisValue(Xbox360Axis.RightThumbX, report.Item4);
        pad.SetAxisValue(Xbox360Axis.RightThumbY, report.Item5);
        pad.SetSliderValue(Xbox360Slider.LeftTrigger, lt);
        pad.SetSliderValue(Xbox360Slider.RightTrigger, rt);
        pad.SubmitReport();
    }

    static ushort Remap(ushort raw, Profile p, bool turboOn)
    {
        var map = p.ButtonMap;
        var turbo = p.TurboButtons;
        ushort result = 0;
        foreach (var (name, mask) in Buttons.All)
        {
            if ((raw & mask) == 0) continue;
            string target = map.TryGetValue(name, out var t) ? t : name;
            if (target == "None") continue;
            if (!turboOn && turbo.Contains(name)) continue;
            result |= Buttons.Mask(target);
        }
        return result;
    }

    static double Norm(short v) => Math.Max(v / 32767.0, -1.0);
    static short ToShort(double v) => (short)Math.Round(Math.Clamp(v, -1.0, 1.0) * 32767);

    void OnFeedback(byte large, byte small)
    {
        _large = large;
        _small = small;
        int slot = PhysicalSlot;
        if (slot < 0) return;
        double k = Profile.Vibration;
        XInput.SetVibration(slot, Scale(large, k), Scale(small, k));
    }

    static ushort Scale(byte v, double k) => (ushort)Math.Min(v * 257 * k, 65535);

    /// <summary>Connects to ViGEmBus and creates our virtual pad. Records which XInput slot it took
    /// so we never read our own output back as "physical" input.</summary>
    void TryConnectBus()
    {
        ViGEmClient? client = null;
        try
        {
            client = new ViGEmClient(); // throws if ViGEmBus is missing
            var before = ConnectedSlots();
            var pad = client.CreateXbox360Controller();
            pad.AutoSubmitReport = false;
            pad.FeedbackReceived += (_, e) => OnFeedback(e.LargeMotor, e.SmallMotor);
            pad.Connect();

            for (int i = 0; i < 30 && _virtualSlot < 0; i++)
            {
                Thread.Sleep(50);
                foreach (var s in ConnectedSlots())
                    if (!before.Contains(s)) _virtualSlot = s;
            }

            _client = client;
            _pad = pad;
            BusStatus = _virtualSlot >= 0
                ? $"ViGEmBus OK - virtual Xbox 360 pad on XInput slot {_virtualSlot + 1}"
                : "ViGEmBus OK - virtual pad created (slot not detected)";
        }
        catch (Exception ex)
        {
            client?.Dispose();
            BusStatus = ex.GetType().Name switch
            {
                "VigemBusNotFoundException" => "ViGEmBus driver not found - install it, then reopen",
                "VigemBusVersionMismatchException" => "ViGEmBus version mismatch - update the driver",
                "VigemBusAccessFailedException" => "Can't access ViGEmBus - try running as administrator",
                _ => "ViGEm error: " + ex.Message,
            };
        }
    }

    int FindPhysical()
    {
        for (int i = 0; i < 4; i++)
            if (i != _virtualSlot && XInput.TryGet(i, out _, out _)) return i;
        return -1;
    }

    static List<int> ConnectedSlots()
    {
        var list = new List<int>();
        for (int i = 0; i < 4; i++)
            if (XInput.TryGet(i, out _, out _)) list.Add(i);
        return list;
    }

    public void Dispose()
    {
        _run = false;
        _thread?.Join(500);
        if (PhysicalSlot >= 0) XInput.SetVibration(PhysicalSlot, 0, 0);
        try { _pad?.Disconnect(); } catch { }
        _client?.Dispose();
        timeEndPeriod(1);
    }
}
