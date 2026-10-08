using Tripwire.Core;
using Xunit;

/// <summary>Udon events beyond the common ones: sync events, frequent events.</summary>
public class EventTests
{
    static TriggerProgram Prog(string eventId, Broadcast broadcast = Broadcast.Local, bool synced = false, params ActionCall[] actions)
    {
        var p = new TriggerProgram();
        if (synced) p.Variables.Add(new VariableDecl { Name = "n", Kind = ValueKind.Int, Initial = 0, Synced = true });
        var e = new EventBlock { EventId = eventId, Broadcast = broadcast };
        e.Actions.Add(new ActionCall { ActionId = "Debug.Log", Args = { ArgValue.Const("x") } });
        e.Actions.AddRange(actions);
        p.Events.Add(e);
        return p;
    }

    static bool Warned(TriggerProgram p) => CodeGenerator.Generate(p).Diagnostics.Exists(d => d.Severity == Severity.Warning);

    [Fact]
    public void SyncEventsNeedASyncedVariable()
    {
        Assert.True(Warned(Prog("OnDeserialization")));
        Assert.False(Warned(Prog("OnDeserialization", synced: true)));
    }

    [Fact]
    public void FrequentEventsShouldStayLocal()
    {
        Assert.True(Warned(Prog("LateUpdate", Broadcast.All)));
        Assert.False(Warned(Prog("LateUpdate")));
    }

    [Fact]
    public void ChangingSyncedVariablesOnReceiveIsWarned()
    {
        var add = new ActionCall { ActionId = "Variable.Add", Args = { ArgValue.Const("n"), ArgValue.Const(1) } };
        Assert.True(Warned(Prog("OnDeserialization", synced: true, actions: add)));
    }

    [Fact]
    public void SyncReceiveAndTheEventShareOneMethod()
    {
        var p = Prog("OnDeserialization", synced: true);
        p.Events.Add(new EventBlock { EventId = "OnVariableChanged", Name = "n", Actions = { new ActionCall { ActionId = "Debug.Log", Args = { ArgValue.Const("n") } } } });
        var g = CodeGenerator.Generate(p);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(g.Source, "void OnDeserialization"));
        Assert.Contains("Tw_SyncReceived();", g.Source);
    }
}

public class LeanOutputTests
{
    [Fact]
    public void UnwatchedVariablesAreSetDirectlyAndSyncNeedsNoReceiveHook()
    {
        // Nobody watches these: no setter for the local one, no change hook or receive detection for the synced one.
        var p = new TriggerProgram();
        p.Variables.Add(new VariableDecl { Name = "local", Kind = ValueKind.Int, Initial = 0 });
        p.Variables.Add(new VariableDecl { Name = "shared", Kind = ValueKind.Int, Initial = 0, Synced = true });
        p.Events.Add(new EventBlock { EventId = "Interact", Actions =
        {
            new ActionCall { ActionId = "Variable.Add", Args = { ArgValue.Const("local"), ArgValue.Const(1) } },
            new ActionCall { ActionId = "Variable.Add", Args = { ArgValue.Const("shared"), ArgValue.Const(1) } },
        } });
        var src = CodeGenerator.Generate(p).Source;
        Assert.Contains("v_local = v_local + 1;", src);
        Assert.DoesNotContain("Tw_Set_local", src);
        Assert.Contains("Tw_Set_shared(v_shared + 1);", src); // ownership and serialization still go through the setter
        Assert.DoesNotContain("Tw_Changed_", src);
        Assert.DoesNotContain("tw_Prev_", src);
        Assert.DoesNotContain("OnDeserialization", src);
    }

    [Fact]
    public void OnlyEventValuesThatAreReadAreCopied()
    {
        var p = new TriggerProgram();
        p.Events.Add(new EventBlock { EventId = "OnPlayerJoined", Actions = { new ActionCall { ActionId = "Debug.Log", Args = { ArgValue.Const("hi") } } } });
        var src = CodeGenerator.Generate(p).Source;
        Assert.DoesNotContain("tw_Arg_OnPlayerJoined_player", src);
        // Read through a text template: kept.
        p.Events[0].Actions[0].Args[0] = ArgValue.Const("hi {player}");
        Assert.Contains("tw_Arg_OnPlayerJoined_player = player;", CodeGenerator.Generate(p).Source);
    }

    [Fact]
    public void SyncReceiveIsWrittenStraightIntoOnDeserialization()
    {
        var p = new TriggerProgram();
        p.Variables.Add(new VariableDecl { Name = "n", Kind = ValueKind.Int, Initial = 0, Synced = true });
        p.Events.Add(new EventBlock { EventId = "OnVariableChanged", Name = "n", Actions = { new ActionCall { ActionId = "Debug.Log", Args = { ArgValue.Const("n") } } } });
        var src = CodeGenerator.Generate(p).Source;
        Assert.Contains("public override void OnDeserialization()\n        {\n            if (v_n != tw_Prev_n)", src);
        Assert.DoesNotContain("Tw_SyncReceived", src);
    }
}

public class ActionRuleTests
{
    [Fact]
    public void VariablePickersMatchWhatTheActionAccepts()
    {
        var toggle = ActionCatalog.Get(ActionCatalog.ToggleVariableId);
        var set = ActionCatalog.Get(ActionCatalog.SetVariableId);
        var each = ActionCatalog.Get(ActionCatalog.ForEachId);
        Assert.True(ActionRules.VariableFits(toggle, toggle.Params[0], ParamType.Of(ValueKind.Bool), false));
        Assert.False(ActionRules.VariableFits(toggle, toggle.Params[0], ParamType.Of(ValueKind.Int), false));
        Assert.True(ActionRules.VariableFits(set, set.Params[0], null, false)); // unresolved type: still offered
        var item = each.Params[1];
        Assert.True(ActionRules.VariableFits(each, item, ParamType.Of(ValueKind.String), false, ParamType.OtherType("System.String[]")));
        Assert.False(ActionRules.VariableFits(each, item, ParamType.Of(ValueKind.String), true, ParamType.OtherType("System.String[]"))); // synced
        Assert.False(ActionRules.VariableFits(each, each.Params[2], ParamType.Of(ValueKind.Float), false)); // round needs an int
    }
}
