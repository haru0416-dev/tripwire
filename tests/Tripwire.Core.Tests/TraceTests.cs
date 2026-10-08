using System.Linq;
using System.Text.RegularExpressions;
using Tripwire.Core;
using Xunit;
using static TestKit;

/// <summary>The notes the editor's event history reads during Play (CodeGenerator.Trace).</summary>
[Collection("inline")]
public class TraceTests
{
    static TriggerProgram Counter()
    {
        var p = new TriggerProgram();
        p.Variables.Add(new VariableDecl { Name = "count", Kind = ValueKind.Int, Initial = 0 });
        p.Variables.Add(new VariableDecl { Name = "step", Kind = ValueKind.Int, Initial = 1, Temporary = true });
        var press = new EventBlock { EventId = "Interact", Actions = { new ActionCall { ActionId = ActionCatalog.AddVariableId, Args = { ArgValue.Const("count"), ArgValue.Var("step") } } } };
        press.Conditions.Add(new Condition { Variable = "count", Op = CompareOp.Less, Value = ArgValue.Const(3) });
        press.Conditions.Add(new Condition { Variable = "step", Op = CompareOp.Greater, Value = ArgValue.Const(0) });
        p.Events.Add(press);
        p.Events.Add(new EventBlock { EventId = EventCatalog.VariableChangedId, Name = "count", Actions = { Log("changed") } });
        return p;
    }

    static string Source(TriggerProgram p, bool tidy = false)
    {
        CodeGenerator.KeepBodiesSeparate = !tidy;
        try { return Regex.Replace(Ok(CodeGenerator.Generate(p)).Source, @"\s+", " "); }
        finally { CodeGenerator.KeepBodiesSeparate = true; }
    }

    [Fact]
    public void AnEventNotesThatItRanOrTheValuesThatStoppedIt()
    {
        var src = Source(Counter());
        // Stopped: the values its conditions compared (not the temporary one: naming it would declare it in the body).
        Assert.Contains("if (v_count >= 3 || v_step <= 0) { if (tw_Trace) Tw_Trace(\"s0\" + \"\\u001fcount\\u001e\" + v_count); return; }", src);
        Assert.Contains("if (tw_Trace) Tw_Trace(\"r0\");", src);
        // A change event notes the new value.
        Assert.Contains("if (tw_Trace) Tw_Trace(\"r1\" + \"\\u001fcount\\u001e\" + v_count);", src);
        Assert.Contains("bool tw_Trace; string[] tw_Log; int tw_LogN;", src);
        Assert.Contains("void Tw_Trace(string note) { tw_Log[tw_LogN % tw_Log.Length] = note; tw_LogN++; }", src);
    }

    [Fact]
    public void AnInlinedBodyKeepsItsNotes()
    {
        var p = new TriggerProgram();
        p.Variables.Add(new VariableDecl { Name = "count", Kind = ValueKind.Int, Initial = 0 });
        var press = new EventBlock { EventId = "Interact", Actions = { Log("a") } };
        press.Conditions.Add(new Condition { Variable = "count", Op = CompareOp.Less, Value = ArgValue.Const(3) });
        p.Events.Add(press);
        var src = Source(p, tidy: true);
        Assert.DoesNotContain("_Tw_E0", src);
        var expected = "if (!(v_count >= 3)) { if (tw_Trace) Tw_Trace(\"r0\"); Debug.Log(\"a\"); } else if (tw_Trace) Tw_Trace(\"s0\" + \"\\u001fcount\\u001e\" + v_count);";
        Assert.Contains(expected, src);
    }

    [Fact]
    public void EventsThatHappenManyTimesASecondAreNotNoted()
    {
        var p = new TriggerProgram();
        p.Events.Add(new EventBlock { EventId = "Update", Actions = { Log("frame") } });
        var src = Source(p);
        Assert.DoesNotContain("tw_Trace", src);
        Assert.DoesNotContain("Tw_Trace", src);
    }

    [Fact]
    public void NotesReadBackAsTheyWereWritten()
    {
        var stopped = CodeGenerator.ParseTrace("s0\u001fcount\u001e3\u001fname\u001eab=c");
        Assert.False(stopped.Ran);
        Assert.Equal(0, stopped.Event);
        Assert.Equal(new[] { ("count", "3"), ("name", "ab=c") }, stopped.Values.Select(kv => (kv.Key, kv.Value)));
        var ran = CodeGenerator.ParseTrace("r12");
        Assert.True(ran.Ran);
        Assert.Equal(12, ran.Event);
        Assert.Empty(ran.Values);
        Assert.Null(CodeGenerator.ParseTrace(null));
        Assert.Null(CodeGenerator.ParseTrace("x1"));
    }

    [Fact]
    public void TheEditorCanRunAWatchedVariablesChangeEvents()
    {
        // Public for the editor (a value changed by hand during Play), '_' so it can't be called over the network.
        var src = Source(Counter());
        Assert.Contains("public void " + CodeGenerator.ChangedMethodOf("count") + "()", src);
        Assert.StartsWith("_", CodeGenerator.ChangedMethodOf("count"));
    }
}
