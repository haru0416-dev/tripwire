using System.Linq;
using Tripwire.Core;
using Xunit;
using static TestKit;

/// <summary>
/// Generated code that costs nothing it doesn't need, and warnings for setups that pile up work or flood the network on
/// events that happen many times a second (found by tests/Tripwire.CostSurvey).
/// </summary>
public class CostTests
{
    static TriggerProgram On(string eventId, ActionCall action, float delay = 0f, bool synced = true)
    {
        var p = new TriggerProgram();
        p.Variables.Add(new VariableDecl { Name = "count", Kind = ValueKind.Int, Initial = 0, Synced = synced });
        p.Events.Add(new EventBlock { EventId = eventId, DelaySeconds = delay, Actions = { action } });
        p.Events.Add(new EventBlock { EventId = EventCatalog.TimerId, Name = "tick", Timer = new TimerSpec(), Actions = { Log("t") } });
        return p;
    }

    static ActionCall Add() => new ActionCall { ActionId = ActionCatalog.AddVariableId, Args = { ArgValue.Const("count"), ArgValue.Const(1) } };

    static string[] Warnings(TriggerProgram p, int ev = 0) =>
        CodeGenerator.Generate(p).Diagnostics.Where(d => d.Severity == Severity.Warning && d.Event == ev).Select(d => d.Message).ToArray();

    [Fact]
    public void SettersNobodyCallsAreNotWritten()
    {
        // Synced, but nothing sets it: no Tw_Set_ (dead code makes the program 10x larger, measured 12 vs 128 instructions).
        var p = new TriggerProgram();
        p.Variables.Add(new VariableDecl { Name = "count", Kind = ValueKind.Int, Initial = 0, Synced = true });
        p.Events.Add(new EventBlock { EventId = EventCatalog.InteractId, Actions = { Log("hi") } });
        Assert.DoesNotContain("Tw_Set_", Flat(p));

        p.Events[0].Actions.Add(Add());
        Assert.Contains("void Tw_Set_count(int value)", Flat(p));
    }

    [Fact]
    public void AVariableOtherTriggersSetKeepsItsSetter()
    {
        // Set through the inbox by another trigger, never by this one.
        var p = new TriggerProgram();
        p.Variables.Add(new VariableDecl { Name = "count", Kind = ValueKind.Int, Initial = 0, Synced = true, External = true });
        p.Events.Add(new EventBlock { EventId = EventCatalog.InteractId, Actions = { Log("hi") } });
        var code = Flat(p);
        Assert.Contains("Tw_Set_count(tw_In_count);", code);
        Assert.Contains("void Tw_Set_count(int value)", code);
    }

    [Fact]
    public void AWatchedVariableStillGetsItsChangeEventWithoutASetter()
    {
        // Received sync runs On Variable Changed even when nothing here sets the variable.
        var p = new TriggerProgram();
        p.Variables.Add(new VariableDecl { Name = "count", Kind = ValueKind.Int, Initial = 0, Synced = true });
        p.Events.Add(new EventBlock { EventId = EventCatalog.VariableChangedId, Name = "count", Actions = { Log("changed") } });
        var code = Flat(p);
        Assert.DoesNotContain("void Tw_Set_count", code);
        Assert.Contains("public void _Tw_Changed_count()", code);
        Assert.Contains("OnDeserialization", code);
    }

    [Fact]
    public void FrequentEventsWarnAboutWorkThatPilesUpOrFloodsTheNetwork()
    {
        Assert.Contains(Warnings(On("Update", Log("x"), delay: 0.5f)), w => w.Contains("pile up") || w.Contains("積み重なります"));
        Assert.Contains(Warnings(On("Update", Add())), w => w.Contains("synced variable") || w.Contains("同期する変数"));
        var timer = new ActionCall { ActionId = ActionCatalog.TimerStartId, Args = { ArgValue.Const("tick") } };
        Assert.Contains(Warnings(On("Update", timer)), w => w.Contains("never fires") || w.Contains("鳴りません"));
        var delayed = new ActionCall { ActionId = ActionCatalog.SendEventDelayedId, Args = { ArgValue.Objs(1), ArgValue.Const("Ping"), ArgValue.Const(1f) } };
        Assert.Contains(Warnings(On("Update", delayed)), w => w.Contains("pile up") || w.Contains("積み重なります"));
        var own = new ActionCall { ActionId = "Networking.TakeOwnership", Args = { ArgValue.Objs(1) } };
        Assert.Contains(Warnings(On("OnPlayerTriggerStay", own)), w => w.Contains("ownership") || w.Contains("オーナー"));
    }

    [Fact]
    public void TheSameSetupsOnOrdinaryEventsDontWarn()
    {
        Assert.Empty(Warnings(On(EventCatalog.InteractId, Log("x"), delay: 0.5f)));
        Assert.Empty(Warnings(On(EventCatalog.InteractId, Add())));
        // An unsynced variable changing every frame sends nothing.
        Assert.Empty(Warnings(On("Update", Add(), synced: false)));
    }

    [Fact]
    public void TheWarningIsOnTheActionThatDoesIt()
    {
        var p = On("Update", Log("first"));
        p.Events[0].Actions.Add(Add());
        var d = CodeGenerator.Generate(p).Diagnostics.Single(x => x.Severity == Severity.Warning && x.Event == 0);
        Assert.Equal(1, d.Action);
    }
}
