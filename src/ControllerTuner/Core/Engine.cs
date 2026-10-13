using System.Diagnostics;
using System.Runtime.InteropServices;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using ControllerTuner.Models;

namespace ControllerTuner.Core;

/// <summary>Reads the physical XInput pad, tunes it, and forwards it to a ViGEm virtual Xbox 360 pad.</summary>
public sealed class Engine : IDisposable
{
    [DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint ms);
    [DllImport("winmm.dll")] static extern uint timeEndPeriod(uint ms);

    ViGEmClient? _client;
    IXbox360Controller? _pad;
    Thread? _thread;
    volatile bool _run;
    int _virtualSlot = -1;
    uint _lastPacket;
    bool _wasActive;

    public Profile Profile { get; set; } = new();
    public volatile bool Bypass;
    public string BusStatus { get; private set; } = "Starting...";
    public bool BusOk => _pad != null;
    public int PhysicalSlot { get; private set; } = -1;
    public int VirtualSlot => _virtualSlot;

    // Left stick live values for the UI preview
    public double InX, InY, OutX, OutY;

    public void Start()
    {
        timeBeginPeriod(1);
        _run = true;
        _thread = new Thread(Run) { IsBackground = true, Name = "pad-loop" };
        _thread.Start();
    }

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

            if (packet != _lastPacket || !_wasActive)
            {
                _lastPacket = packet;
                _wasActive = true;
                Send(pad, gp);
            }
            Thread.Sleep(1);
        }
    }

    void Send(IXbox360Controller pad, XInput.Gamepad g)
    {
        var p = Profile;
        double lx = Norm(g.ThumbLX), ly = Norm(g.ThumbLY), rx = Norm(g.ThumbRX), ry = Norm(g.ThumbRY);
        byte lt = g.LeftTrigger, rt = g.RightTrigger;
        InX = lx; InY = ly;

        if (!Bypass)
        {
            (lx, ly) = Processor.Stick(lx, ly, p.LeftStick);
            (rx, ry) = Processor.Stick(rx, ry, p.RightStick);
            lt = Processor.Trigger(lt, p.TriggerDeadzone, p.TriggerSensitivity);
            rt = Processor.Trigger(rt, p.TriggerDeadzone, p.TriggerSensitivity);
            if (p.SwapSticks) (lx, ly, rx, ry) = (rx, ry, lx, ly);
        }
        OutX = lx; OutY = ly;

        pad.SetButtonsFull(g.Buttons);
        pad.SetAxisValue(Xbox360Axis.LeftThumbX, ToShort(lx));
        pad.SetAxisValue(Xbox360Axis.LeftThumbY, ToShort(ly));
        pad.SetAxisValue(Xbox360Axis.RightThumbX, ToShort(rx));
        pad.SetAxisValue(Xbox360Axis.RightThumbY, ToShort(ry));
        pad.SetSliderValue(Xbox360Slider.LeftTrigger, lt);
        pad.SetSliderValue(Xbox360Slider.RightTrigger, rt);
        pad.SubmitReport();
    }

    static double Norm(short v) => Math.Max(v / 32767.0, -1.0);
    static short ToShort(double v) => (short)Math.Round(Math.Clamp(v, -1.0, 1.0) * 32767);

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
        try { _pad?.Disconnect(); } catch { }
        _client?.Dispose();
        timeEndPeriod(1);
    }
}
