using System.Linq;
using System.Text.RegularExpressions;
using Tripwire.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using static TestKit;

/// <summary>"If" blocks inside the actions, and how conditions combine (all / any, not).</summary>
public class BranchTests
{
    static ActionCall Toggle(int objects = 1) => new ActionCall { ActionId = "GameObject.ToggleActive", Args = { ArgValue.Objs(objects) } };
    static Condition Is(string v, CompareOp op, object value, bool not = false) => new Condition { Variable = v, Op = op, Value = ArgValue.Const(value), Negate = not };

    static ActionCall If(Condition[] conditions, bool any, ActionCall[] then, ActionCall[] otherwise = null)
    {
        var a = new ActionCall { ActionId = ActionCatalog.IfId, MatchAny = any };
        a.Conditions.AddRange(conditions);
        a.Then.AddRange(then);
        if (otherwise != null) a.Else.AddRange(otherwise);
        return a;
    }

    static TriggerProgram Program(params ActionCall[] actions)
    {
        var p = new TriggerProgram();
        p.Variables.Add(new VariableDecl { Name = "hasKey", Kind = ValueKind.Bool, Initial = false });
        p.Variables.Add(new VariableDecl { Name = "n", Kind = ValueKind.Int, Initial = 0 });
        var e = new EventBlock { EventId = "Interact" };
        e.Actions.AddRange(actions);
        p.Events.Add(e);
        return p;
    }

    [Fact]
    public void IfElseRunsOneSideOrTheOther()
    {
        // The key-and-door case: open with the key, otherwise say so.
        var src = Flat(CodeGenerator.Generate(Program(If(new[] { Is("hasKey", CompareOp.Equal, true) }, false, new[] { Toggle() }, new[] { Log("no key") }))));
        Assert.Matches(@"if \(v_hasKey\) \{ if \(Utilities\.IsValid\(tw_A0_1_targets\)\) [^}]*\} else \{ Debug\.Log\(""no key""\); \}", src);
    }

    [Theory]
    [InlineData(false, false, "v_hasKey && v_n >= 3")]   // all
    [InlineData(true, false, "v_hasKey || v_n >= 3")]    // any
    [InlineData(false, true, "!v_hasKey && v_n >= 3")]   // all, first one negated
    public void ConditionsCombine(bool any, bool notFirst, string expected)
    {
        var block = If(new[] { Is("hasKey", CompareOp.Equal, true, notFirst), Is("n", CompareOp.GreaterOrEqual, 3) }, any, new[] { Log("ok") });
        Assert.Contains("if (" + expected + ")", Flat(CodeGenerator.Generate(Program(block))));
    }

    [Theory]
    [InlineData(false, "if (!v_hasKey || v_n < 3) return;")] // all must hold: fail when any fails
    [InlineData(true, "if (!v_hasKey && v_n < 3) return;")]  // any one holds: fail only when all fail
    public void EventConditionsCanMatchAny(bool any, string expected)
    {
        var p = Program(Log("x"));
        p.Events[0].MatchAny = any;
        p.Events[0].Conditions.Add(Is("hasKey", CompareOp.Equal, true));
        p.Events[0].Conditions.Add(Is("n", CompareOp.GreaterOrEqual, 3));
        Assert.Contains(expected, Flat(CodeGenerator.Generate(p)));
    }

    [Fact]
    public void BlocksNestAndNumberTheirActionsInOrder()
    {
        // Numbering: block 0, its Then (1: inner block, 2: its Then toggle), its Else (3: toggle); 4: after the block.
        var inner = If(new[] { Is("n", CompareOp.Equal, 1) }, false, new[] { Toggle() });
        var g = CodeGenerator.Generate(Program(If(new[] { Is("hasKey", CompareOp.Equal, true) }, false, new[] { inner }, new[] { Toggle(2) }), Toggle()));
        var src = Flat(g);
        Assert.Contains("if (v_hasKey) { if (v_n == 1) { if (Utilities.IsValid(tw_A0_2_targets))", src);
        Assert.Equal(new[] { 2, 3, 4 }, g.Bindings.Select(b => b.Action).OrderBy(x => x));
        Assert.True(g.Bindings.Single(b => b.Action == 3).IsArray);
    }

    [Fact]
    public void OnlyAnElseSideTestsTheNegation()
    {
        var src = Flat(CodeGenerator.Generate(Program(If(new[] { Is("hasKey", CompareOp.Equal, true) }, false, new ActionCall[0], new[] { Log("no key") }))));
        Assert.Contains("if (!v_hasKey) { Debug.Log(\"no key\"); }", src);
        Assert.DoesNotContain("else", src);
    }

    [Fact]
    public void ProblemsInsideBlocksPointAtTheNestedAction()
    {
        var empty = CodeGenerator.Generate(Program(If(new Condition[0], false, new[] { Log("x") })));
        Assert.Contains(empty.Diagnostics, d => d.Severity == Severity.Error && d.Action == 0);
        var badInner = CodeGenerator.Generate(Program(If(new[] { Is("hasKey", CompareOp.Equal, true) }, false, new[] { new ActionCall { ActionId = "GameObject.ToggleActive", Args = { ArgValue.Objs(0) } } })));
        Assert.Contains(badInner.Diagnostics, d => d.Action == 1); // the toggle inside, not the block
        var badCondition = CodeGenerator.Generate(Program(If(new[] { Is("missing", CompareOp.Equal, true) }, false, new[] { Log("x") })));
        Assert.Contains(badCondition.Diagnostics, d => d.Severity == Severity.Error && d.Action == 0 && d.Condition == 0);
    }
}

public class BranchNumberingTests
{
    [Fact]
    public void TheSameActionTwiceStillGenerates()
    {
        // Numbers come from positions, so one ActionCall object used twice is fine.
        var log = new ActionCall { ActionId = "Debug.Log", Args = { ArgValue.Const("x") } };
        var p = new TriggerProgram();
        p.Events.Add(new EventBlock { EventId = "Interact", Actions = { log, log } });
        Assert.False(CodeGenerator.Generate(p).HasErrors);
    }

    [Fact]
    public void OnlyBlocksHaveContents()
    {
        // Then / Else on a non-block action are ignored and take no numbers.
        var stray = new ActionCall { ActionId = "Debug.Log", Args = { ArgValue.Const("x") } };
        stray.Then.Add(new ActionCall { ActionId = "Debug.Log", Args = { ArgValue.Const("hidden") } });
        var toggle = new ActionCall { ActionId = "GameObject.ToggleActive", Args = { ArgValue.Objs(1) } };
        var p = new TriggerProgram();
        p.Events.Add(new EventBlock { EventId = "Interact", Actions = { stray, toggle } });
        var g = CodeGenerator.Generate(p);
        Assert.Equal(1, Assert.Single(g.Bindings).Action);
        Assert.DoesNotContain("hidden", g.Source);
        Assert.Equal(2, ActionCall.Flatten(p.Events[0].Actions).Count);
    }
}
