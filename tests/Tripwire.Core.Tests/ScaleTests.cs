using System.Diagnostics;
using Tripwire.Core;
using Xunit;
using static TestKit;

/// <summary>
/// The Inspector generates the program on every pass, so generating must stay quick for big triggers. The loop
/// check once looked up each block's links by scanning all of them at every step: 144 blocks took 75 ms here and
/// several times that under the editor's Mono.
/// </summary>
public class ScaleTests
{
    /// <summary>Blocks that watch, toggle and call each other: many links, many cycles.</summary>
    static TriggerProgram Busy(int copies)
    {
        var p = new TriggerProgram();
        p.Variables.Add(new VariableDecl { Name = "open", Kind = ValueKind.Bool, Initial = false, Synced = true });
        p.Variables.Add(new VariableDecl { Name = "count", Kind = ValueKind.Int, Initial = 0 });
        for (int c = 0; c < copies; c++)
        {
            p.Events.Add(new EventBlock { EventId = "Interact", Actions = { new ActionCall { ActionId = ActionCatalog.ToggleVariableId, Args = { ArgValue.Const("open") } } } });
            p.Events.Add(new EventBlock { EventId = EventCatalog.VariableChangedId, Name = "open", Actions = {
                new ActionCall { ActionId = "GameObject.SetActive", Args = { ArgValue.Objs(1), ArgValue.Var("open") } },
                new ActionCall { ActionId = ActionCatalog.AddVariableId, Args = { ArgValue.Const("count"), ArgValue.Const(1) } } } });
            p.Events.Add(new EventBlock { EventId = "OnPlayerTriggerEnter", Actions = { new ActionCall { ActionId = "GameObject.ToggleActive", Args = { ArgValue.Objs(2) } } } });
            p.Events.Add(new EventBlock { EventId = EventCatalog.CustomId, Name = "Ping" + c, Conditions = { new Condition { Variable = "count", Op = CompareOp.Less, Value = ArgValue.Const(5) } },
                Actions = { Log("x"), new ActionCall { ActionId = ActionCatalog.SendEventId, Args = { ArgValue.SelfObject(), ArgValue.Const("Ping" + c), ArgValue.Const(0) } } } });
        }
        return p;
    }

    [Fact]
    public void ABigTriggerGeneratesQuickly()
    {
        var p = Busy(36); // 144 blocks
        // As the editor generates: single-use bodies inlined (the tests keep them separate to read the code).
        var kept = CodeGenerator.KeepBodiesSeparate;
        CodeGenerator.KeepBodiesSeparate = false;
        try
        {
            var g = CodeGenerator.Generate(p);
            Assert.Contains(g.Diagnostics, d => d.Message.Contains("止まらずに続きます")); // the loops are still found
            Assert.DoesNotContain("void _Tw_E0()", g.Source); // the first click's body went into Interact
            var w = Stopwatch.StartNew();
            for (int i = 0; i < 5; i++) CodeGenerator.Generate(p);
            Assert.True(w.Elapsed.TotalMilliseconds / 5 < 30, "generating 144 blocks took " + (w.Elapsed.TotalMilliseconds / 5).ToString("F1") + " ms");
        }
        finally { CodeGenerator.KeepBodiesSeparate = kept; }
    }
}
