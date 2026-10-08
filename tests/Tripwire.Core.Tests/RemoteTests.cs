using Tripwire.Core;
using Xunit;
using static TestKit;

/// <summary>Set / Read Another Trigger's Variable, and the receiving side.</summary>
public class RemoteTests
{
    static TriggerProgram Caller(ActionCall a)
    {
        var p = new TriggerProgram();
        p.Variables.Add(new VariableDecl { Name = "mine", Kind = ValueKind.Int, Initial = 0 });
        p.Events.Add(new EventBlock { EventId = "Interact", Actions = { a } });
        return p;
    }

    static ActionCall Remote(string id, ParamType type, ArgValue third) => new ActionCall
    {
        ActionId = id,
        Args = { ArgValue.Objs(1), ArgValue.Const("点数"), third },
        RemoteType = type,
    };

    [Fact]
    public void SettingGoesThroughTheOtherTriggersSetter()
    {
        var src = Flat(Caller(Remote(ActionCatalog.SetRemoteId, ParamType.Of(ValueKind.Int), ArgValue.Const(7))));
        var id = CodeGenerator.Ident("点数");
        // Both statements under the validity guard.
        Assert.Contains("if (Utilities.IsValid(tw_A0_0_trigger)) { tw_A0_0_trigger.SetProgramVariable(\"tw_In_" + id + "\", 7); tw_A0_0_trigger.SendCustomEvent(\"_Tw_Take_" + id + "\"); }", src);
    }

    [Fact]
    public void ReadingTakesTheField()
    {
        var src = Flat(Caller(Remote(ActionCatalog.GetRemoteId, ParamType.Of(ValueKind.Int), ArgValue.Const("mine"))));
        Assert.Contains("v_mine = (int)tw_A0_0_trigger.GetProgramVariable(\"v_" + CodeGenerator.Ident("点数") + "\");", src);
    }

    [Fact]
    public void TheReceiverTakesValuesThroughItsSetter()
    {
        var p = new TriggerProgram();
        p.Variables.Add(new VariableDecl { Name = "score", Kind = ValueKind.Int, Initial = 0, Synced = true, External = true });
        var src = Flat(p);
        Assert.Contains("public int tw_In_score;", src);
        Assert.Contains("public void _Tw_Take_score() { Tw_Set_score(tw_In_score); }", src); // ownership and sync as usual
    }

    [Fact]
    public void UnknownOrMismatchedVariablesAreReported()
    {
        Assert.True(CodeGenerator.Generate(Caller(Remote(ActionCatalog.SetRemoteId, null, ArgValue.Const(1)))).HasErrors);
        Assert.True(CodeGenerator.Generate(Caller(Remote(ActionCatalog.GetRemoteId, ParamType.Of(ValueKind.String), ArgValue.Const("mine")))).HasErrors);
    }
}

public class TemporaryVariableTests
{
    static TriggerProgram Prog(VariableDecl v, params EventBlock[] events)
    {
        var p = new TriggerProgram();
        p.Variables.Add(v);
        p.Events.AddRange(events);
        return p;
    }

    [Fact]
    public void EventsThatUseItStartFromItsInitialValue()
    {
        var v = new VariableDecl { Name = "step", Kind = ValueKind.Int, Initial = 1, Temporary = true };
        var uses = new EventBlock { EventId = "Interact", Actions = { new ActionCall { ActionId = ActionCatalog.AddVariableId, Args = { ArgValue.Const("step"), ArgValue.Const(1) } } } };
        var other = new EventBlock { EventId = "Custom", Name = "Other", Actions = { Log("x") } };
        var src = Flat(Prog(v, uses, other));
        // A local of each event that uses it: an event it calls has its own copy.
        Assert.DoesNotContain("public int v_step", src);
        Assert.Contains("void _Tw_E0() { int v_step = 1; v_step = v_step + 1; }", src);
        Assert.Contains("void _Tw_E1() { Debug.Log(\"x\"); }", src); // doesn't use it
    }

    [Fact]
    public void ATemporaryVariableCantBeWatched()
    {
        var v = new VariableDecl { Name = "t", Kind = ValueKind.Int, Initial = 0, Temporary = true };
        Assert.True(CodeGenerator.Generate(Prog(v, new EventBlock { EventId = "OnVariableChanged", Name = "t", Actions = { Log("x") } })).HasErrors);
    }

    [Theory]
    [InlineData(true, false)]  // synced
    [InlineData(false, true)]  // a list
    public void OnlyUnsyncedValuesCanBeTemporary(bool synced, bool list)
    {
        var v = new VariableDecl { Name = "x", Type = list ? ParamType.Objects("UnityEngine.GameObject") : ParamType.Of(ValueKind.Int), Synced = synced, Temporary = true };
        Assert.True(CodeGenerator.Generate(Prog(v)).HasErrors);
    }

    [Fact]
    public void AnotherTriggersTemporaryVariableIsOutOfReach()
    {
        var p = new TriggerProgram();
        p.Variables.Add(new VariableDecl { Name = "mine", Kind = ValueKind.Int, Initial = 0 });
        p.Events.Add(new EventBlock { EventId = "Interact", Actions =
        {
            new ActionCall { ActionId = ActionCatalog.GetRemoteId, Args = { ArgValue.Objs(1), ArgValue.Const("t"), ArgValue.Const("mine") }, RemoteType = ParamType.Of(ValueKind.Int), RemoteTemporary = true },
        } });
        Assert.True(CodeGenerator.Generate(p).HasErrors);
    }
}

public class ContinuousSyncTests
{
    [Fact]
    public void ContinuousSyncSmoothsWhatUdonCanInterpolate()
    {
        var p = new TriggerProgram { ContinuousSync = true };
        p.Variables.Add(new VariableDecl { Name = "pos", Kind = ValueKind.Vector3, Synced = true, Interpolate = true });
        p.Variables.Add(new VariableDecl { Name = "on", Kind = ValueKind.Bool, Initial = false, Synced = true });
        var g = Ok(CodeGenerator.Generate(p));
        Assert.Equal("Continuous", g.SyncMode);
        Assert.Contains("[UdonSynced(UdonSyncMode.Linear)] public Vector3 v_pos", g.Source);
        Assert.Contains("[UdonSynced] public bool v_on", g.Source);
    }

    [Fact]
    public void ListsCantBeSentContinuously()
    {
        var p = new TriggerProgram { ContinuousSync = true };
        p.Variables.Add(new VariableDecl { Name = "nums", Type = ParamType.OtherType("System.Int32[]"), Synced = true });
        Assert.True(CodeGenerator.Generate(p).HasErrors);
    }
}
