using System.Collections.Generic;
using System.Linq;
using Tripwire.Core;
using Xunit;
using static TestKit;

/// <summary>Values typed in the Inspector go into fields the editor fills (as the editor generates), not into the code.</summary>
public class ConstantFieldTests
{
    static GeneratedProgram Fields(TriggerProgram p)
    {
        CodeGenerator.ConstantsInFields = true;
        try { return Ok(CodeGenerator.Generate(p)); }
        finally { CodeGenerator.ConstantsInFields = false; }
    }

    static TriggerProgram Click(params ActionCall[] actions)
    {
        var p = new TriggerProgram();
        var e = new EventBlock { EventId = "Interact" };
        e.Actions.AddRange(actions);
        p.Events.Add(e);
        return p;
    }

    static object Bound(GeneratedProgram g, string field) => g.Bindings.Single(b => b.Field == field && b.Kind == BindingKind.Constant).Constant;

    [Fact]
    public void AValueIsAFieldNamedAfterItsPlace()
    {
        var g = Fields(Click(new ActionCall { ActionId = "Transform.SetPosition", Args = { ArgValue.Objs(1), ArgValue.Const(new[] { 1f, 2f, 3f }) } }));
        Assert.Contains("public Vector3 tw_C0_0_1;", g.Source);
        Assert.Contains(".position = tw_C0_0_1;", g.Source);
        Assert.Equal(new[] { 1f, 2f, 3f }, Bound(g, "tw_C0_0_1"));
    }

    [Fact]
    public void TwoTriggersThatDifferOnlyInValuesShareTheirCode()
    {
        TriggerProgram Label(string text) => Click(new ActionCall { ActionId = "Text.SetText", Args = { ArgValue.Objs(1), ArgValue.Const(text) } });
        var a = Fields(Label("Room 1"));
        var b = Fields(Label("Room 2"));
        Assert.Equal(a.ClassName, b.ClassName);
        Assert.Equal("Room 2", Bound(b, "tw_C0_0_1"));
    }

    [Fact]
    public void TheTextBetweenPlaceholdersIsInFieldsAndThePlaceholdersInTheCode()
    {
        var p = Click(new ActionCall { ActionId = "Text.SetText", Args = { ArgValue.Objs(1), ArgValue.Const("{count} 点 / {count}") } });
        p.Variables.Add(new VariableDecl { Name = "count", Kind = ValueKind.Int, Initial = 3 });
        var g = Fields(p);
        Assert.Contains("\"\" + v_count.ToString() + tw_C0_0_1_t1 + v_count.ToString()", Flat(g.Source));
        Assert.Equal(" 点 / ", Bound(g, "tw_C0_0_1_t1"));
        // The variable's initial value too: no initializer, the editor fills the field.
        Assert.Contains("public int v_count;", g.Source);
        Assert.Equal(3, Bound(g, "v_count"));
    }

    [Fact]
    public void ARotationIsStoredAsItsQuaternion()
    {
        var p = Click(new ActionCall { ActionId = ActionCatalog.SetVariableId, Args = { ArgValue.Const("turn"), ArgValue.Const(new[] { 0f, 90f, 0f }) } });
        p.Variables.Add(new VariableDecl { Name = "turn", Kind = ValueKind.Quaternion });
        var q = (float[])Bound(Fields(p), "tw_C0_0_1");
        Assert.Equal(UnityMath.Euler(0f, 90f, 0f), q);
    }

    [Fact]
    public void WhatPicksCodeStaysInTheCode()
    {
        // An enum member, and on/off in a condition (written as x or !x).
        var p = Click(new ActionCall { ActionId = ActionCatalog.LogId, Args = { ArgValue.Const("x") } });
        p.Variables.Add(new VariableDecl { Name = "on", Kind = ValueKind.Bool });
        p.Events[0].Conditions.Add(new Condition { Variable = "on", Op = CompareOp.Equal, Value = ArgValue.Const(false) });
        var g = Fields(p);
        Assert.Contains("if (v_on) return;", Flat(g.Source));
        Assert.DoesNotContain(g.Bindings, b => b.Kind == BindingKind.Constant && b.Constant is bool && b.Field.StartsWith("tw_C"));
    }

    [Fact]
    public void ListNamesAreFilledInToo()
    {
        var p = Click(Log("x"));
        p.Events[0].Gate = Gate.InList;
        p.Events[0].GateNames = new List<string> { "haru", "alice" };
        var g = Fields(p);
        Assert.Contains("public string[] tw_Gate0;", g.Source);
        Assert.Equal(new[] { "haru", "alice" }, Bound(g, "tw_Gate0"));
    }

    static string Flat(string source) => System.Text.RegularExpressions.Regex.Replace(WithoutTrace(source), @"\s+", " ");

    [Fact]
    public void ARotationThatIsNotANumberIsFilledInAsTheLiteralWouldBe()
    {
        var p = Click(Log("x"));
        p.Variables.Add(new VariableDecl { Name = "r", Kind = ValueKind.Quaternion, Initial = new[] { float.NaN, 90f, 0f } });
        // The literal turns the angles into a rotation, then each part that isn't a number into 0.
        Assert.Contains("public Quaternion v_r = new Quaternion(0f, 0f, 0f, 0f);", CodeGenerator.Generate(p).Source);
        Assert.Equal(new[] { 0f, 0f, 0f, 0f }, Bound(Fields(p), "v_r"));
    }
}
