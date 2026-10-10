using System.Text.RegularExpressions;
using Tripwire.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using static TestKit;

/// <summary>Repeat, For Each, Break, Return.</summary>
public class LoopTests
{
    static ActionCall Loop(string id, ActionCall[] body, params ArgValue[] args)
    {
        var a = new ActionCall { ActionId = id };
        a.Args.AddRange(args);
        a.Then.AddRange(body);
        return a;
    }

    static TriggerProgram Prog(params ActionCall[] actions)
    {
        var p = new TriggerProgram();
        p.Variables.Add(new VariableDecl { Name = "i", Kind = ValueKind.Int, Initial = 0 });
        p.Variables.Add(new VariableDecl { Name = "names", Type = ParamType.OtherType("System.String[]") });
        p.Variables.Add(new VariableDecl { Name = "name", Kind = ValueKind.String, Initial = "" });
        p.Variables.Add(new VariableDecl { Name = "shared", Kind = ValueKind.Int, Initial = 0, Synced = true });
        p.Events.Add(new EventBlock { EventId = "Interact" });
        p.Events[0].Actions.AddRange(actions);
        return p;
    }

    [Fact]
    public void ACounterMissingFromTheSavedDataIsJustNotUsed()
    {
        // The counter is optional: null (the editor's "no input saved") counts like an empty one; a missing count is an error.
        Assert.False(CodeGenerator.Generate(Prog(Loop(ActionCatalog.RepeatId, new[] { Log("x") }, ArgValue.Const(5), null))).HasErrors);
        Assert.True(CodeGenerator.Generate(Prog(Loop(ActionCatalog.RepeatId, new[] { Log("x") }, null, ArgValue.Const("")))).HasErrors);
    }

    [Fact]
    public void RepeatCountsIntoAVariable()
    {
        var src = Flat(Prog(Loop(ActionCatalog.RepeatId, new[] { Log("x") }, ArgValue.Const(5), ArgValue.Const("i"))));
        Assert.Contains("int tw_N0 = 5; for (int tw_L0 = 0; tw_L0 < tw_N0; tw_L0++) { v_i = tw_L0; Debug.Log(\"x\"); }", src);
    }

    [Fact]
    public void ForEachWalksACopyOfTheList()
    {
        var src = Flat(Prog(Loop(ActionCatalog.ForEachId, new[] { Log("x") }, ArgValue.Const("names"), ArgValue.Const("name"), ArgValue.Const(""))));
        Assert.Contains("System.String[] tw_A0 = v_names; if (tw_A0 != null) for (int tw_L0 = 0; tw_L0 < tw_A0.Length; tw_L0++) { v_name = tw_A0[tw_L0]; Debug.Log(\"x\"); }", src);
    }

    [Fact]
    public void LeaveTheLoopAndStopHere()
    {
        var leave = new ActionCall { ActionId = "Flow.Break" };
        var stop = new ActionCall { ActionId = "Flow.Stop" };
        var src = Flat(Prog(Loop(ActionCatalog.RepeatId, new[] { Log("a"), leave }, ArgValue.Const(2), ArgValue.Const("")), stop, Log("never")));
        Assert.Contains("Debug.Log(\"a\"); break; } return; Debug.Log(\"never\");", src);
        // The action after Return gets a warning (action 3: the loop, its two actions, stop, then this one).
        var g = CodeGenerator.Generate(Prog(Loop(ActionCatalog.RepeatId, new[] { Log("a"), leave }, ArgValue.Const(2), ArgValue.Const("")), stop, Log("never")));
        Assert.Contains(g.Diagnostics, d => d.Action == 4 && d.Severity == Severity.Warning);
        Assert.Single(g.Diagnostics);
    }

    [Fact]
    public void NestedLoopsUseTheirOwnCounters()
    {
        var inner = Loop(ActionCatalog.RepeatId, new[] { Log("x") }, ArgValue.Const(2), ArgValue.Const(""));
        var src = Flat(Prog(Loop(ActionCatalog.RepeatId, new[] { inner }, ArgValue.Const(3), ArgValue.Const(""))));
        Assert.Contains("tw_L0 < tw_N0", src);
        Assert.Contains("tw_L1 < tw_N1", src);
    }

    [Theory]
    [InlineData("leave outside a loop")]
    [InlineData("synced counter")]
    [InlineData("item of the wrong type")]
    [InlineData("not a list")]
    [InlineData("item and round in one variable")]
    public void MistakesAreReported(string mistake)
    {
        var p = mistake switch
        {
            "leave outside a loop" => Prog(new ActionCall { ActionId = "Flow.Break" }),
            "synced counter" => Prog(Loop(ActionCatalog.RepeatId, new[] { Log("x") }, ArgValue.Const(2), ArgValue.Const("shared"))),
            "item of the wrong type" => Prog(Loop(ActionCatalog.ForEachId, new[] { Log("x") }, ArgValue.Const("names"), ArgValue.Const("i"), ArgValue.Const(""))),
            "item and round in one variable" => Prog(Loop(ActionCatalog.ForEachId, new[] { Log("x") }, ArgValue.Const("names"), ArgValue.Const("name"), ArgValue.Const("name"))),
            _ => Prog(Loop(ActionCatalog.ForEachId, new[] { Log("x") }, ArgValue.Const("name"), ArgValue.Const("name"), ArgValue.Const(""))),
        };
        Assert.True(CodeGenerator.Generate(p).HasErrors);
    }
}

public class FlowTests
{
    static Condition Is(string v, CompareOp op, object value) => new Condition { Variable = v, Op = op, Value = ArgValue.Const(value) };

    static ActionCall Block(string id, Condition[] conditions, ActionCall[] then, ActionCall[] otherwise = null)
    {
        var a = new ActionCall { ActionId = id };
        a.Conditions.AddRange(conditions);
        a.Then.AddRange(then);
        if (otherwise != null) a.Else.AddRange(otherwise);
        return a;
    }

    static TriggerProgram Prog(params ActionCall[] actions)
    {
        var p = new TriggerProgram();
        p.Variables.Add(new VariableDecl { Name = "n", Kind = ValueKind.Int, Initial = 0 });
        p.Events.Add(new EventBlock { EventId = "Interact" });
        p.Events[0].Actions.AddRange(actions);
        return p;
    }

    static ActionCall Add(int k) => new ActionCall { ActionId = ActionCatalog.AddVariableId, Args = { ArgValue.Const("n"), ArgValue.Const(k) } };

    [Fact]
    public void OtherwiseIfChainsAsElseIf()
    {
        var chain = Block(ActionCatalog.IfId, new[] { Is("n", CompareOp.Equal, 1) }, new[] { Log("one") },
            new[] { Block(ActionCatalog.IfId, new[] { Is("n", CompareOp.Equal, 2) }, new[] { Log("two") }, new[] { Log("other") }) });
        var src = Flat(Prog(chain));
        Assert.Contains("if (v_n == 1) { Debug.Log(\"one\"); } else if (v_n == 2) { Debug.Log(\"two\"); } else { Debug.Log(\"other\"); }", src);
    }

    [Fact]
    public void RepeatWhileLoopsAndSkipsAhead()
    {
        var skip = new ActionCall { ActionId = ActionCatalog.ContinueId };
        var loop = Block(ActionCatalog.WhileId, new[] { Is("n", CompareOp.Less, 10) }, new[] { Add(1), skip, Log("never") });
        var g = CodeGenerator.Generate(Prog(loop));
        var src = Flat(g);
        Assert.Contains("while (v_n < 10) { v_n = v_n + 1; continue; Debug.Log(\"never\"); }", src);
        Assert.DoesNotContain(g.Diagnostics, d => d.Message.Contains("終わらない") || d.Message.Contains("never end"));
    }

    [Fact]
    public void AWhileThatNothingEndsIsWarned()
    {
        var loop = Block(ActionCatalog.WhileId, new[] { Is("n", CompareOp.Less, 10) }, new[] { Log("forever") });
        var g = CodeGenerator.Generate(Prog(loop));
        Assert.False(g.HasErrors);
        Assert.Contains(g.Diagnostics, d => d.Severity == Severity.Warning && d.Action == 0);
    }

    [Fact]
    public void SkippingOutsideALoopIsAnError()
    {
        Assert.True(CodeGenerator.Generate(Prog(new ActionCall { ActionId = ActionCatalog.ContinueId })).HasErrors);
    }
}

public class WhileEndingTests
{
    static TriggerProgram Prog(ActionCall loop)
    {
        var p = new TriggerProgram();
        p.Variables.Add(new VariableDecl { Name = "n", Kind = ValueKind.Int, Initial = 0 });
        p.Variables.Add(new VariableDecl { Name = "m", Kind = ValueKind.Int, Initial = 5 });
        p.Events.Add(new EventBlock { EventId = "Interact", Actions = { loop } });
        return p;
    }

    static ActionCall While(Condition c, params ActionCall[] body)
    {
        var a = new ActionCall { ActionId = ActionCatalog.WhileId };
        a.Conditions.Add(c);
        a.Then.AddRange(body);
        return a;
    }

    static bool WarnsNeverEnds(TriggerProgram p) => CodeGenerator.Generate(p).Diagnostics.Exists(d => d.Severity == Severity.Warning && d.Action == 0);

    [Fact]
    public void AnInnerLoopsLeaveDoesNotEndTheOuterLoop()
    {
        var inner = new ActionCall { ActionId = ActionCatalog.RepeatId, Args = { ArgValue.Const(3), ArgValue.Const("") } };
        inner.Then.Add(new ActionCall { ActionId = ActionCatalog.BreakId });
        Assert.True(WarnsNeverEnds(Prog(While(new Condition { Variable = "n", Op = CompareOp.Less, Value = ArgValue.Const(3) }, inner))));
    }

    [Fact]
    public void ChangingTheRightHandVariableCanEndIt()
    {
        var down = new ActionCall { ActionId = ActionCatalog.AddVariableId, Args = { ArgValue.Const("m"), ArgValue.Const(-1) } };
        Assert.False(WarnsNeverEnds(Prog(While(new Condition { Variable = "n", Op = CompareOp.Less, Value = ArgValue.Var("m") }, down))));
    }
}
