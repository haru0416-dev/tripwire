using Tripwire.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using static TestKit;

/// <summary>How values reach parameters: casts, guards, whole arrays, value-type receivers.</summary>
public class ValueTests
{
    static ParamType GoArray() => new ParamType(ValueKind.Object, "UnityEngine.GameObject", true);
    static ParamType PlayerArray() => new ParamType(ValueKind.Player, null, true);

    static TriggerProgram SetProgram(VariableDecl target, ArgValue value, string eventId = "OnTriggerEnter", params VariableDecl[] more)
    {
        var p = new TriggerProgram();
        p.Variables.Add(target);
        p.Variables.AddRange(more);
        var e = new EventBlock { EventId = eventId };
        e.Actions.Add(new ActionCall { ActionId = "Variable.Set", Args = { ArgValue.Const(target.Name), value } });
        p.Events.Add(e);
        return p;
    }

    [Fact]
    public void IntVariableIntoFloatParameterIsCast()
    {
        var p = new TriggerProgram();
        p.Variables.Add(new VariableDecl { Name = "lo", Kind = ValueKind.Int });
        p.Variables.Add(new VariableDecl { Name = "r", Kind = ValueKind.Float });
        var e = new EventBlock { EventId = "Interact" };
        e.Actions.Add(new ActionCall
        {
            ActionId = ActionCatalog.CallId,
            ResultVariable = "r",
            Call = new CallSpec
            {
                UdonName = "UnityEngineRandom.__Range__SystemSingle_SystemSingle__SystemSingle",
                DeclaringType = "UnityEngine.Random", Member = "Range", Kind = CallKind.Method,
                Params = { new EventParam("minInclusive", ParamType.Of(ValueKind.Float)), new EventParam("maxInclusive", ParamType.Of(ValueKind.Float)) },
                Returns = ParamType.Of(ValueKind.Float),
            },
            Args = { ArgValue.Var("lo"), ArgValue.Const(1f) },
        });
        p.Events.Add(e);
        var g = CodeGenerator.Generate(p);
        Assert.False(g.HasErrors);
        Assert.Contains("UnityEngine.Random.Range((float)v_lo, 1f)", g.Source);
    }

    [Fact]
    public void ThisGameObjectCannotProvideAnAsset()
    {
        var p = new TriggerProgram();
        var e = new EventBlock { EventId = "Interact" };
        e.Actions.Add(new ActionCall { ActionId = "AudioSource.PlayOneShot", Args = { ArgValue.Objs(1), ArgValue.SelfObject() } });
        p.Events.Add(e);
        var g = CodeGenerator.Generate(p);
        Assert.Contains(g.Diagnostics, d => d.Severity == Severity.Error && d.Action == 0 && d.Arg == 1);

        p.Events[0].Actions[0] = new ActionCall { ActionId = "AudioSource.Play", Args = { ArgValue.SelfObject() } };
        Assert.False(CodeGenerator.Generate(p).HasErrors);
    }

    [Fact]
    public void ArrayVariablesOnlyTakeWholeArrays()
    {
        var doors = new VariableDecl { Name = "doors", Type = GoArray() };
        Assert.True(CodeGenerator.Generate(SetProgram(doors, ArgValue.SelfObject())).HasErrors);
        Assert.True(CodeGenerator.Generate(SetProgram(doors, ArgValue.Param("other"))).HasErrors);
        Assert.True(CodeGenerator.Generate(SetProgram(doors, ArgValue.Var("one"), more: new VariableDecl { Name = "one", Type = ParamType.Object("UnityEngine.GameObject") })).HasErrors);

        var players = new VariableDecl { Name = "ps", Type = PlayerArray() };
        Assert.True(CodeGenerator.Generate(SetProgram(players, ArgValue.Local())).HasErrors);

        Assert.False(CodeGenerator.Generate(SetProgram(doors, ArgValue.Objs(2))).HasErrors);
        Assert.False(CodeGenerator.Generate(SetProgram(players, ArgValue.Var("others"), more: new VariableDecl { Name = "others", Type = PlayerArray() })).HasErrors);
    }

    [Fact]
    public void ValueTypeInstancesAreParenthesized()
    {
        var p = new TriggerProgram();
        p.Variables.Add(new VariableDecl { Name = "i", Kind = ValueKind.Int });
        p.Variables.Add(new VariableDecl { Name = "s", Kind = ValueKind.String });
        p.Variables.Add(new VariableDecl { Name = "r", Kind = ValueKind.Int });
        var toString = new CallSpec { UdonName = "SystemSingle.__ToString__SystemString", DeclaringType = "System.Single", Member = "ToString", Kind = CallKind.Method, Instance = ParamType.Of(ValueKind.Float), Returns = ParamType.Of(ValueKind.String) };
        var compare = new CallSpec { UdonName = "SystemSingle.__CompareTo__SystemSingle__SystemInt32", DeclaringType = "System.Single", Member = "CompareTo", Kind = CallKind.Method, Instance = ParamType.Of(ValueKind.Float), Params = { new EventParam("value", ParamType.Of(ValueKind.Float)) }, Returns = ParamType.Of(ValueKind.Int) };
        var e = new EventBlock { EventId = "Interact" };
        e.Actions.Add(new ActionCall { ActionId = ActionCatalog.CallId, Call = toString, ResultVariable = "s", Args = { ArgValue.Var("i") } });
        e.Actions.Add(new ActionCall { ActionId = ActionCatalog.CallId, Call = compare, ResultVariable = "r", Args = { ArgValue.Const(-2f), ArgValue.Const(-3f) } });
        p.Events.Add(e);

        var g = CodeGenerator.Generate(p);
        Assert.False(g.HasErrors);
        Assert.Contains("v_s = ((float)v_i).ToString();", g.Source);
        Assert.Contains("v_r = (-2f).CompareTo(-3f);", g.Source);
        AssertParses(g);
    }

    [Fact]
    public void SetVariableKeepsTheGuardOfADereferencedEventParam()
    {
        var go = new VariableDecl { Name = "go", Type = ParamType.Object("UnityEngine.GameObject") };
        var g = CodeGenerator.Generate(SetProgram(go, ArgValue.Param("other")));
        Assert.False(g.HasErrors);
        Assert.Contains("if (Utilities.IsValid(tw_Arg_OnTriggerEnter_other)) v_go = tw_Arg_OnTriggerEnter_other.gameObject;", g.Source);

        // The same event value as an action target (Collider → its GameObject) is guarded too.
        var p = new TriggerProgram();
        p.Events.Add(new EventBlock { EventId = "OnTriggerEnter", Actions = { new ActionCall { ActionId = "GameObject.SetActive", Args = { ArgValue.Param("other"), ArgValue.Const(false) } } } });
        Assert.Contains("if (Utilities.IsValid(tw_Arg_OnTriggerEnter_other)) tw_Arg_OnTriggerEnter_other.gameObject.SetActive(false);", CodeGenerator.Generate(p).Source);
    }
}

public class NarrowIntTests
{
    static TriggerProgram WithByteArg(ArgValue value, VariableDecl v = null)
    {
        var p = new TriggerProgram();
        if (v != null) p.Variables.Add(v);
        var e = new EventBlock { EventId = "Interact" };
        var call = new CallSpec
        {
            UdonName = "usharp:X::m:PlayUrl", DeclaringType = "JLChnToZ.VRC.VVMW.Core", Member = "PlayUrl", Kind = CallKind.Method,
            Instance = new ParamType(ValueKind.Object, "JLChnToZ.VRC.VVMW.Core", true) { IsComponent = true },
        };
        call.Params.Add(new EventParam("playerType", new ParamType(ValueKind.Int, "System.Byte")));
        e.Actions.Add(new ActionCall { ActionId = ActionCatalog.ScriptCallId, Call = call, Args = { ArgValue.Objs(1), value } });
        p.Events.Add(e);
        return p;
    }

    [Fact]
    public void OutOfRangeConstantsAreErrors()
    {
        Assert.True(CodeGenerator.Generate(WithByteArg(ArgValue.Const(300))).HasErrors);
        Assert.True(CodeGenerator.Generate(WithByteArg(ArgValue.Const(-1))).HasErrors);
        Assert.False(CodeGenerator.Generate(WithByteArg(ArgValue.Const(255))).HasErrors);
    }

    [Fact]
    public void ByteVariablesStillFit()
    {
        var g = CodeGenerator.Generate(WithByteArg(ArgValue.Var("pt"), new VariableDecl { Name = "pt", Type = ParamType.OtherType("System.Byte") }));
        Assert.False(g.HasErrors, string.Join("\n", g.Diagnostics));
        Assert.Contains(".PlayUrl(v_pt);", g.Source);
        var ints = CodeGenerator.Generate(WithByteArg(ArgValue.Var("n"), new VariableDecl { Name = "n", Type = ParamType.Of(ValueKind.Int) }));
        Assert.False(ints.HasErrors, string.Join("\n", ints.Diagnostics));
        Assert.Contains(".PlayUrl(((System.Byte)(v_n)));", ints.Source);
    }
}
