using Tripwire.Core;
using Xunit;
using static TestKit;

/// <summary>The final tidy-up the editor's output gets: single-use bodies inlined, component lookups shared.</summary>
[Collection("inline")]
public class InlineTests
{
    static string Tidy(TriggerProgram p)
    {
        CodeGenerator.KeepBodiesSeparate = false;
        try { return Flat(p); }
        finally { CodeGenerator.KeepBodiesSeparate = true; }
    }

    static TriggerProgram Door()
    {
        var p = new TriggerProgram();
        p.Variables.Add(new VariableDecl { Name = "open", Kind = ValueKind.Bool, Initial = false });
        var on = new EventBlock { EventId = "Interact", Actions = { Log("a") } };
        on.Conditions.Add(new Condition { Variable = "open", Op = CompareOp.Equal, Value = ArgValue.Const(true) });
        var off = new EventBlock { EventId = "Interact", Actions = { Log("b") } };
        off.Conditions.Add(new Condition { Variable = "open", Op = CompareOp.Equal, Value = ArgValue.Const(false) });
        p.Events.Add(on);
        p.Events.Add(off);
        return p;
    }

    [Fact]
    public void SingleUseBodiesAreWrittenIntoTheirCaller()
    {
        var src = Tidy(Door());
        // Both blocks of the click: each its own condition, no separate methods left.
        Assert.Contains("public override void Interact() { if (v_open) { Debug.Log(\"a\"); } if (!v_open) { Debug.Log(\"b\"); } }", src);
        Assert.DoesNotContain("_Tw_E", src);
    }

    [Fact]
    public void BodiesWithAReturnOrSeveralCallersStaySeparate()
    {
        var p = Door();
        p.Events[0].Actions.Add(new ActionCall { ActionId = ActionCatalog.StopId });
        var src = Tidy(p);
        Assert.Contains("void _Tw_E0()", src); // a Return inside would leave the caller
        Assert.DoesNotContain("void _Tw_E1()", src);
    }

    [Fact]
    public void LoopsOfDifferentEventsKeepTheirOwnLocals()
    {
        var p = new TriggerProgram();
        ActionCall Loop() { var a = new ActionCall { ActionId = ActionCatalog.RepeatId, Args = { ArgValue.Const(2), ArgValue.Const("") } }; a.Then.Add(Log("x")); return a; }
        p.Events.Add(new EventBlock { EventId = "Interact", Actions = { Loop() } });
        p.Events.Add(new EventBlock { EventId = "Interact", Actions = { Loop() } });
        var src = Tidy(p); // Flat checks it parses; both blocks use tw_L0 in their own braces
        Assert.Contains("{ int tw_N0 = 2;", src);
    }

    [Fact]
    public void AComponentOfThisObjectIsLookedUpOnce()
    {
        var p = new TriggerProgram();
        p.Events.Add(new EventBlock { EventId = "Interact", Actions = { new ActionCall { ActionId = "AudioSource.Play", Args = { ArgValue.SelfObject() } } } });
        var src = Tidy(p);
        Assert.Contains("UnityEngine.AudioSource tw_Self0 = GetComponent<UnityEngine.AudioSource>();", src);
        Assert.Contains("if (Utilities.IsValid(tw_Self0)) tw_Self0.Play();", src);
    }

    /// <summary>Every called body exists, and no method ended up inside another.</summary>
    static void AssertWellFormed(string raw)
    {
        foreach (System.Text.RegularExpressions.Match call in System.Text.RegularExpressions.Regex.Matches(raw, @"\b(_Tw_E\d+)\(\);"))
            Assert.Contains("void " + call.Groups[1].Value + "()", raw);
        Assert.DoesNotMatch(@"\n {12,}(public )?(override )?void \w+\(", raw);
    }

    [Fact]
    public void EmptyEventsDontSwallowTheNextMethod()
    {
        var p = new TriggerProgram();
        p.Variables.Add(new VariableDecl { Name = "n", Kind = ValueKind.Int, Initial = 0, Synced = true });
        p.Events.Add(new EventBlock { EventId = "Interact" }); // just added: nothing in it yet
        p.Events.Add(new EventBlock { EventId = "Start", Actions = { Log("x"), new ActionCall { ActionId = "AudioSource.Play", Args = { ArgValue.SelfObject() } }, new ActionCall { ActionId = "AudioSource.Stop", Args = { ArgValue.SelfObject() } } } });
        p.Events.Add(new EventBlock { EventId = "OnEnable", DelaySeconds = 1f });
        p.Events.Add(new EventBlock { EventId = "Custom", Name = "Bump", Actions = { new ActionCall { ActionId = ActionCatalog.AddVariableId, Args = { ArgValue.Const("n"), ArgValue.Const(1) } } } });
        CodeGenerator.KeepBodiesSeparate = false;
        try { AssertWellFormed(Ok(CodeGenerator.Generate(p)).Source); }
        finally { CodeGenerator.KeepBodiesSeparate = true; }
    }

    [Fact]
    public void TextsMentioningGetComponentStayAsTheyAre()
    {
        var p = new TriggerProgram();
        p.Events.Add(new EventBlock { EventId = "Interact", Actions =
        {
            new ActionCall { ActionId = "AudioSource.Play", Args = { ArgValue.SelfObject() } },
            Log("GetComponent<UnityEngine.AudioSource>()"),
        } });
        var src = Tidy(p);
        Assert.Contains("Debug.Log(\"GetComponent<UnityEngine.AudioSource>()\")", src);
    }
}
