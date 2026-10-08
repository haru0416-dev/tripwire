using System.Linq;
using Tripwire.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using static TestKit;

public class TextTests
{
    static GeneratedProgram SetText(ArgValue text, string eventId = "Interact", params VariableDecl[] vars)
    {
        var p = new TriggerProgram();
        p.Variables.AddRange(vars);
        p.Events.Add(new EventBlock { EventId = eventId, Actions = { new ActionCall { ActionId = "Text.SetText", Args = { ArgValue.Objs(1), text } } } });
        return CodeGenerator.Generate(p);
    }

    static VariableDecl Var(string name, ValueKind kind, object initial = null) => new VariableDecl { Name = name, Kind = kind, Initial = initial };

    [Fact]
    public void PlayersShowTheirNameNullSafely()
    {
        var p = SetText(ArgValue.Var("who"), vars: new VariableDecl { Name = "who", Type = ParamType.Of(ValueKind.Player) });
        Ok(p);
        Assert.Contains("(Utilities.IsValid(v_who) ? v_who.displayName : \"\")", p.Source); // a left player must not halt the behaviour
    }

    [Theory]
    [InlineData("{nope}")]
    [InlineData("open {score")]
    [InlineData("close }")]
    public void BadTemplatesAreReported(string text) =>
        Assert.True(SetText(ArgValue.Const(text), vars: Var("score", ValueKind.Int, 0)).HasErrors);

    [Fact]
    public void ArraysAreNotText() =>
        Assert.True(SetText(ArgValue.Var("list"), vars: new VariableDecl { Name = "list", Type = new ParamType(ValueKind.Int, null, true) }).HasErrors);

    [Fact]
    public void BracesStayLiteralOutsideDisplayTexts()
    {
        // A condition comparing to "{n}", and an event name with braces: unchanged, no error.
        var p = new TriggerProgram();
        p.Variables.Add(new VariableDecl { Name = "s", Kind = ValueKind.String, Initial = "" });
        p.Variables.Add(new VariableDecl { Name = "n", Kind = ValueKind.Int, Initial = 0 });
        var e = new EventBlock { EventId = "Interact", Actions = { new ActionCall { ActionId = "Variable.Set", Args = { ArgValue.Const("s"), ArgValue.Const("{}") } } } };
        e.Conditions.Add(new Condition { Variable = "s", Op = CompareOp.Equal, Value = ArgValue.Const("{n}") });
        p.Events.Add(e);
        var g = CodeGenerator.Generate(p);
        Ok(g);
        Assert.Contains("if (v_s != \"{n}\") return;", WithoutTrace(g.Source));
        Assert.Contains("v_s = \"{}\";", g.Source);
    }

    public static TheoryData<string, string, string, string[], string> Templates => new TheoryData<string, string, string, string[], string>
    {
        // action, event, text, int variables, expected expression
        { "Text.SetText", "Interact", "スコア: {score}点 / {score}", new[] { "score" }, "\"スコア: \" + v_score.ToString() + \"点 / \" + v_score.ToString()" },
        { "Text.SetText", "OnPlayerJoined", "{player}さん（{人数}人目）", new[] { "人数" }, " : \"\") + \"さん（\" + v_uni_" },
        { "Text.SetText", "Interact", "{{score}} = {score}", new[] { "score" }, "\"{score} = \" + v_score.ToString()" },
        { "Text.SetText", "Interact", "[{ score}]", new[] { " score" }, ".ToString() + \"]\"" }, // a name with a space at an end
        { "Debug.Log", "OnPlayerJoined", "joined: {player}", new string[0], "Debug.Log(\"joined: \" + (Utilities.IsValid(" },
    };

    [Theory]
    [MemberData(nameof(Templates))]
    public void TemplatesInsertVariablesAndEventValues(string action, string eventId, string text, string[] ints, string expected)
    {
        var p = new TriggerProgram();
        foreach (var v in ints) p.Variables.Add(Var(v, ValueKind.Int, 0));
        var args = action == "Text.SetText" ? new[] { ArgValue.Objs(1), ArgValue.Const(text) } : new[] { ArgValue.Const(text) };
        var call = new ActionCall { ActionId = action };
        call.Args.AddRange(args);
        p.Events.Add(new EventBlock { EventId = eventId, Actions = { call } });
        var g = CodeGenerator.Generate(p);
        Ok(g);
        Assert.Contains(expected, g.Source);
    }
}
