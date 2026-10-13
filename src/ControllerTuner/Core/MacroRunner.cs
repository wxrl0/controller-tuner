using System.Linq;
using ControllerTuner.Models;

namespace ControllerTuner.Core;

/// <summary>Plays the profile's macros. Called from the input thread only.</summary>
public sealed class MacroRunner
{
    sealed class State
    {
        public bool Running, Prev, InGap;
        public int Step;
        public long StepStart;
    }

    readonly Dictionary<Macro, State> _states = new();
    Profile? _profile;
    volatile bool _active;

    /// <summary>True while any macro is playing (the engine must keep ticking).</summary>
    public bool Active => _active;

    public void Reset()
    {
        _states.Clear();
        _active = false;
    }

    /// <summary>Returns buttons to add to the output, and trigger buttons to hide from the game.</summary>
    public (ushort Add, ushort Suppress) Update(Profile p, ushort raw, long now)
    {
        if (!ReferenceEquals(p, _profile))
        {
            _profile = p;
            _states.Clear();
        }

        ushort add = 0, suppress = 0;
        bool any = false;

        foreach (var m in p.Macros)
        {
            var steps = m.Steps;
            ushort trig = Buttons.Mask(m.Trigger);
            if (trig == 0 || steps.Count == 0) continue;

            if (!_states.TryGetValue(m, out var st)) _states[m] = st = new State();

            bool down = (raw & trig) != 0;
            bool pressed = down && !st.Prev;
            st.Prev = down;
            suppress |= trig;

            switch (m.Mode)
            {
                case "Once":
                    if (pressed) Start(st, now);
                    break;
                case "Hold":
                    if (down && !st.Running) Start(st, now);
                    else if (!down) st.Running = false;
                    break;
                case "Toggle":
                    if (pressed)
                    {
                        if (st.Running) st.Running = false;
                        else Start(st, now);
                    }
                    break;
            }

            if (st.Running) Advance(m, steps, st, now);

            if (st.Running)
            {
                any = true;
                if (!st.InGap && st.Step < steps.Count)
                    foreach (var name in steps[st.Step].Press) add |= Buttons.Mask(name);
            }
        }

        _active = any;
        return (add, suppress);
    }

    static void Start(State st, long now)
    {
        st.Running = true;
        st.Step = 0;
        st.InGap = false;
        st.StepStart = now;
    }

    static void Advance(Macro m, List<MacroStep> steps, State st, long now)
    {
        for (int guard = 0; st.Running && guard < 1000; guard++)
        {
            if (st.Step >= steps.Count) st.Step = 0; // steps were edited while running
            var step = steps[st.Step];
            long dur = Math.Max(1, st.InGap ? step.WaitMs : step.HoldMs);

            if (now - st.StepStart < dur) return;
            if (now - st.StepStart > dur + 1000) st.StepStart = now - dur; // long stall: don't fast-forward
            st.StepStart += dur;

            if (!st.InGap)
            {
                st.InGap = true;
                continue;
            }

            st.InGap = false;
            st.Step++;
            if (st.Step >= steps.Count)
            {
                st.Step = 0;
                if (m.Mode == "Once") st.Running = false;
            }
        }
    }
}

/// <summary>Turns a recorded timeline of button states into macro steps.</summary>
public static class Recorder
{
    public static List<MacroStep> ToSteps(List<(long T, ushort Mask)> ev)
    {
        var steps = new List<MacroStep>();

        for (int i = 0; i < ev.Count - 1 && steps.Count < 200; i++)
        {
            long dur = Math.Max(ev[i + 1].T - ev[i].T, 1);
            ushort mask = ev[i].Mask;

            if (mask != 0)
            {
                var press = Buttons.All.Where(b => (mask & b.Mask) != 0).Select(b => b.Name).ToList();
                steps.Add(new MacroStep { Press = press, HoldMs = (int)Math.Min(dur, 60000), WaitMs = 0 });
            }
            else if (steps.Count > 0)
            {
                var prev = steps[steps.Count - 1];
                prev.WaitMs = (int)Math.Min(prev.WaitMs + dur, 60000);
            }
        }

        // Don't keep the idle time between the last press and clicking Stop.
        if (steps.Count > 0)
        {
            var last = steps[steps.Count - 1];
            last.WaitMs = Math.Min(last.WaitMs, 100);
        }

        return steps;
    }
}
