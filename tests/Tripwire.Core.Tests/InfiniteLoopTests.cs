using System.Collections.Generic;
using System.Linq;
using Tripwire.Core;
using Xunit;
using static TestKit;

/// <summary>Event blocks that run each other without end, and the loops people build on purpose (which stay quiet).</summary>
public class InfiniteLoopTests
{
    static EventBlock Custom(string name, params ActionCall[] actions)
    {
        var b = new EventBlock { EventId = EventCatalog.CustomId, Name = name };
        b.Actions.AddRange(actions);
        return b;
    }

    static ActionCall Send(string name, int broadcast = 0) =>
        new ActionCall { ActionId = ActionCatalog.SendEventId, Args = { ArgValue.SelfObject(), ArgValue.Const(name), ArgValue.Const(broadcast) } };

    static ActionCall SendLater(string name) =>
        new ActionCall { ActionId = ActionCatalog.SendEventDelayedId, Args = { ArgValue.SelfObject(), ArgValue.Const(name), ArgValue.Const(1f) } };

    static ActionCall Toggle(string v) => new ActionCall { ActionId = ActionCatalog.ToggleVariableId, Args = { ArgValue.Const(v) } };

    static Diagnostic[] Warnings(TriggerProgram p) =>
        CodeGenerator.Generate(p).Diagnostics.Where(d => d.Severity == Severity.Warning).ToArray();

    [Fact]
    public void AnEventCallingItselfRightAwayIsReported()
    {
        var p = new TriggerProgram { Events = { Custom("Ping", Send("Ping")) } };
        var w = Assert.Single(Warnings(p));
        Assert.Equal(0, w.Event);
        Assert.Equal(0, w.Action);
        Assert.Contains("すぐにまた自分を動かす", w.Message); // a block on its own: no "A → A" path
    }

    [Fact]
    public void TwoEventsCallingEachOtherAreReportedOnce()
    {
        var p = new TriggerProgram { Events = { Custom("A", Log("a"), Send("B")), Custom("B", Send("A")) } };
        var w = Assert.Single(Warnings(p));
        Assert.Equal(0, w.Event);
        Assert.Equal(1, w.Action); // the Send, not the Log before it
    }

    [Fact]
    public void ChangingTheWatchedVariableInsideItsOwnChangeEventIsReported()
    {
        var p = new TriggerProgram();
        p.Variables.Add(new VariableDecl { Name = "on", Kind = ValueKind.Bool });
        p.Events.Add(new EventBlock { EventId = EventCatalog.VariableChangedId, Name = "on", Actions = { Toggle("on") } });
        Assert.Single(Warnings(p));
    }

    [Fact]
    public void SettingTheSameValueAgainStopsSoItIsNotReported()
    {
        var p = new TriggerProgram();
        p.Variables.Add(new VariableDecl { Name = "on", Kind = ValueKind.Bool });
        var set = new ActionCall { ActionId = ActionCatalog.SetVariableId, Args = { ArgValue.Const("on"), ArgValue.Const(true) } };
        p.Events.Add(new EventBlock { EventId = EventCatalog.VariableChangedId, Name = "on", Actions = { set } });
        Assert.Empty(Warnings(p));
    }

    [Fact]
    public void AnEventThatCallsItselfLaterIsATimerNotALoop()
    {
        Assert.Empty(Warnings(new TriggerProgram { Events = { Custom("Tick", Log("tick"), SendLater("Tick")) } }));
        // A delay on the block itself breaks the chain too.
        var delayed = Custom("Ping", Send("Ping"));
        delayed.DelaySeconds = 1f;
        Assert.Empty(Warnings(new TriggerProgram { Events = { delayed } }));
    }

    [Fact]
    public void CallingItselfLaterTwiceDoublesAndIsReported()
    {
        var w = Assert.Single(Warnings(new TriggerProgram { Events = { Custom("Tick", SendLater("Tick"), SendLater("Tick")) } }));
        Assert.Equal(1, w.Action);
        Assert.Contains("倍", w.Message.Replace("double", "倍"));
    }

    [Fact]
    public void SendingToEveryoneInACircleIsReportedAsNetwork()
    {
        var w = Assert.Single(Warnings(new TriggerProgram { Events = { Custom("Ping", Send("Ping", broadcast: 1)) } }));
        Assert.True(w.Message.Contains("network") || w.Message.Contains("ネットワーク"), w.Message);
    }

    [Fact]
    public void ACallThatGoesNowhereBackIsQuiet()
    {
        Assert.Empty(Warnings(new TriggerProgram { Events = { Custom("A", Send("B")), Custom("B", Log("b")) } }));
    }
}

/// <summary>Blocks that all call each other: the loop search stays fast and the warnings few.</summary>
public class LoopSearchBoundTests
{
    [Fact]
    public void ManyBlocksCallingEachOtherStayFast()
    {
        var p = new TriggerProgram();
        for (int i = 0; i < 14; i++)
            p.Events.Add(new EventBlock { EventId = EventCatalog.CustomId, Name = "Same",
                Actions = { new ActionCall { ActionId = ActionCatalog.SendEventId, Args = { ArgValue.SelfObject(), ArgValue.Const("Same"), ArgValue.Const(0) } } } });
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var g = CodeGenerator.Generate(p);
        Assert.True(watch.ElapsedMilliseconds < 1000, $"took {watch.ElapsedMilliseconds} ms");
        var loops = g.Diagnostics.Count(d => d.Severity == Severity.Warning);
        Assert.InRange(loops, 1, 60); // a few representative loops, not hundreds of thousands
    }

    [Fact]
    public void ABusyClusterDoesNotHideALoopElsewhere()
    {
        // Blocks 0..8 all call each other (more cycles than the limit), and 9 and 10 call each other.
        var links = new Dictionary<int, List<int>>();
        for (int i = 0; i < 9; i++) links[i] = Enumerable.Range(0, 9).Where(j => j != i).ToList();
        links[9] = new List<int> { 10 }; links[10] = new List<int> { 9 };
        var cycles = CodeGenerator.Cycles(Enumerable.Range(0, 11), n => links[n]);
        Assert.Contains(cycles, c => c.SequenceEqual(new[] { 9, 10 }));
    }

    [Fact]
    public void AWideTreeBeforeALoopStillFindsIt()
    {
        // Block 0 fans out 8 wide over 5 levels (no loop there), and the last level leads into a loop.
        var links = new Dictionary<int, List<int>>();
        int nextId = 1; var level = new List<int> { 0 };
        for (int depth = 0; depth < 5; depth++)
        {
            var below = Enumerable.Range(nextId, 8).ToList(); nextId += 8;
            foreach (var n in level) links[n] = below;
            level = below;
        }
        int a = nextId, b = nextId + 1;
        foreach (var n in level) links[n] = new List<int> { a };
        links[a] = new List<int> { b }; links[b] = new List<int> { a };
        var cycles = CodeGenerator.Cycles(Enumerable.Range(0, b + 1), n => links.TryGetValue(n, out var l) ? l : new List<int>());
        Assert.Contains(cycles, c => c.SequenceEqual(new[] { a, b }));
    }
}
