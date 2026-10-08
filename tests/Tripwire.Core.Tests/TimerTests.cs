using System.Text.RegularExpressions;
using Tripwire.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using static TestKit;

/// <summary>Timer events, Start / Stop Timer, Update, random numbers.</summary>
public class TimerTests
{
    static EventBlock Timer(string name, float min, float max, bool repeat = true, bool auto = true) =>
        new EventBlock { EventId = "Timer", Name = name, Timer = new TimerSpec { MinSeconds = min, MaxSeconds = max, Repeat = repeat, AutoStart = auto },
            Actions = { new ActionCall { ActionId = "Debug.Log", Args = { ArgValue.Const("tick") } } } };

    [Fact]
    public void RepeatingTimerStartsAtStartAndReschedulesItself()
    {
        var p = new TriggerProgram();
        p.Events.Add(Timer("blink", 2f, 2f));
        var src = Flat(p);
        Assert.Contains("void Start() { Tw_StartTimers(); }", src);
        Assert.Contains("float tw_D = 2f; tw_TimerDue0 = UnityEngine.Time.time + tw_D; SendCustomEventDelayedSeconds(\"_Tw_T0\", tw_D);", src);
        // A tick runs only while running and when due (an earlier one made stale by a restart does nothing);
        // it schedules the next one before running the actions.
        Assert.Contains("public void _Tw_T0() { if (!tw_TimerOn0 || UnityEngine.Time.time < tw_TimerDue0 - 0.02f) return; Tw_Schedule0(); _Tw_E0(); }", src);
    }

    [Fact]
    public void RandomIntervalsAndOneShotTimers()
    {
        var p = new TriggerProgram();
        p.Events.Add(Timer("flash", 5f, 10f, repeat: false));
        var src = Flat(p);
        Assert.Contains("UnityEngine.Random.Range(5f, 10f)", src);
        Assert.Contains("tw_TimerOn0 = false; _Tw_E0();", src); // once: stops before running
    }

    [Fact]
    public void StartAndStopTimerActions()
    {
        var p = new TriggerProgram();
        p.Events.Add(Timer("countdown", 1f, 1f, auto: false));
        p.Events.Add(new EventBlock { EventId = "Interact", Actions = { new ActionCall { ActionId = "Timer.Start", Args = { ArgValue.Const("countdown") } } } });
        p.Events.Add(new EventBlock { EventId = "Custom", Name = "Halt", Actions = { new ActionCall { ActionId = "Timer.Stop", Args = { ArgValue.Const("countdown") } } } });
        var src = Flat(p);
        Assert.DoesNotContain("Tw_StartTimers", src); // not started automatically
        Assert.DoesNotContain("void Start()", src);
        Assert.Contains("tw_TimerOn0 = true; Tw_Schedule0();", src);
        Assert.Contains("tw_TimerOn0 = false;", src);
    }

    [Fact]
    public void ExistingStartAlsoStartsTimers()
    {
        var p = new TriggerProgram();
        p.Events.Add(new EventBlock { EventId = "Start", Actions = { new ActionCall { ActionId = "Debug.Log", Args = { ArgValue.Const("s") } } } });
        p.Events.Add(Timer("t", 1f, 1f));
        var src = Flat(p);
        Assert.Single(Regex.Matches(src, @"void Start\(\)"));
        Assert.Contains("void Start() { Tw_StartTimers(); _Tw_E0();", src);
    }

    [Theory]
    [InlineData(0f, 1f)]   // no time
    [InlineData(3f, 2f)]   // longest shorter than shortest
    public void BadTimesAreReported(float min, float max)
    {
        var p = new TriggerProgram();
        p.Events.Add(Timer("t", min, max));
        Assert.True(CodeGenerator.Generate(p).HasErrors);
    }

    [Fact]
    public void UnknownTimersAndDuplicateNamesAreReported()
    {
        var p = new TriggerProgram();
        p.Events.Add(Timer("t", 1f, 1f));
        p.Events.Add(Timer("t", 1f, 1f));
        Assert.True(CodeGenerator.Generate(p).HasErrors);
        var q = new TriggerProgram();
        q.Events.Add(new EventBlock { EventId = "Interact", Actions = { new ActionCall { ActionId = "Timer.Stop", Args = { ArgValue.Const("nope") } } } });
        Assert.True(CodeGenerator.Generate(q).HasErrors);
    }

    [Fact]
    public void RandomNumbersIncludeTheMaximumForWholeNumbers()
    {
        var p = new TriggerProgram();
        p.Variables.Add(new VariableDecl { Name = "dice", Kind = ValueKind.Int, Initial = 0 });
        p.Variables.Add(new VariableDecl { Name = "f", Kind = ValueKind.Float, Initial = 0f });
        p.Events.Add(new EventBlock { EventId = "Interact", Actions =
        {
            new ActionCall { ActionId = "Variable.Random", Args = { ArgValue.Const("dice"), ArgValue.Const(1), ArgValue.Const(6) } },
            new ActionCall { ActionId = "Variable.Random", Args = { ArgValue.Const("f"), ArgValue.Const(0f), ArgValue.Const(1f) } },
        } });
        var src = Flat(p);
        Assert.Contains("v_dice = UnityEngine.Random.Range(1, (6) + 1);", src);
        Assert.Contains("v_f = UnityEngine.Random.Range(0f, 1f);", src);
    }

    [Fact]
    public void ATimerEventDoesNotReserveTheNameTimer()
    {
        var p = new TriggerProgram();
        p.Events.Add(new EventBlock { EventId = "Custom", Name = "Timer", Actions = { new ActionCall { ActionId = "Debug.Log", Args = { ArgValue.Const("x") } } } });
        Flat(p);
    }

    [Theory]
    [InlineData(1, int.MaxValue)]
    [InlineData(6, 1)]
    public void ImpossibleRandomRangesAreReported(int min, int max)
    {
        var p = new TriggerProgram();
        p.Variables.Add(new VariableDecl { Name = "n", Kind = ValueKind.Int, Initial = 0 });
        p.Events.Add(new EventBlock { EventId = "Interact", Actions = { new ActionCall { ActionId = "Variable.Random", Args = { ArgValue.Const("n"), ArgValue.Const(min), ArgValue.Const(max) } } } });
        Assert.True(CodeGenerator.Generate(p).HasErrors);
    }

    [Fact]
    public void UpdateRunsEveryFrame()
    {
        var p = new TriggerProgram();
        p.Events.Add(new EventBlock { EventId = "Update", Actions = { new ActionCall { ActionId = "Debug.Log", Args = { ArgValue.Const("f") } } } });
        Assert.Contains("void Update() { _Tw_E0(); }", Flat(p));
    }
}
