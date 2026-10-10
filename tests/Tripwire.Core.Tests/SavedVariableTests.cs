using System.Linq;
using Tripwire.Core;
using Xunit;
using static TestKit;

/// <summary>Saved variables: each player's value kept in PlayerData and given back when they come again.</summary>
public class SavedVariableTests
{
    static TriggerProgram Program(VariableDecl v, params EventBlock[] events)
    {
        var p = new TriggerProgram();
        p.Variables.Add(v);
        p.Events.AddRange(events);
        return p;
    }

    static EventBlock AddOne(string name) => new EventBlock { EventId = "Interact", Actions = { new ActionCall { ActionId = ActionCatalog.AddVariableId, Args = { ArgValue.Const(name), ArgValue.Const(1) } } } };

    [Fact]
    public void AChangeIsSavedOnceTheDataHasComeBack()
    {
        var src = Flat(Program(new VariableDecl { Name = "coins", Kind = ValueKind.Int, Initial = 0, SaveKey = "coins" }, AddOne("coins")));
        Assert.Contains("Tw_Set_coins(", src);
        Assert.Contains("if (tw_Restored) VRC.SDK3.Persistence.PlayerData.SetInt(\"coins\", value);", src);
        Assert.Contains("public override void OnPlayerRestored(VRCPlayerApi player)", src);
        Assert.Contains("if (VRC.SDK3.Persistence.PlayerData.TryGetInt(player, \"coins\", out tw_S)) Tw_Set_coins(tw_S);", src);
        Assert.Contains("tw_Restored = true;", src);
    }

    [Fact]
    public void ARestoredEventOfItsOwnRunsAfterTheValuesCameBack()
    {
        var p = Program(new VariableDecl { Name = "coins", Kind = ValueKind.Int, Initial = 0, SaveKey = "wallet" },
            new EventBlock { EventId = "OnPlayerRestored", PlayerFilter = PlayerFilter.LocalPlayer, Actions = { Log("{coins}") } });
        var src = Flat(p);
        var method = src.Substring(src.IndexOf("public override void OnPlayerRestored", System.StringComparison.Ordinal));
        Assert.True(method.IndexOf("Tw_Restore(player);", System.StringComparison.Ordinal) < method.IndexOf("_Tw_E0", System.StringComparison.Ordinal) || !method.Contains("_Tw_E0"),
            "the values come back before the card runs");
        Assert.Contains("\"wallet\"", src);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(src, "void OnPlayerRestored"));
    }

    [Fact]
    public void ASavedVariableCantBeSyncedTemporaryOrAList()
    {
        Assert.Contains(CodeGenerator.Generate(Program(new VariableDecl { Name = "x", Kind = ValueKind.Int, SaveKey = "x", Synced = true }, AddOne("x"))).Diagnostics,
            d => d.Severity == Severity.Error && d.Variable == 0);
        var list = ParamType.Of(ValueKind.Int); list.IsArray = true;
        Assert.Contains(CodeGenerator.Generate(Program(new VariableDecl { Name = "x", Type = list, SaveKey = "x" })).Diagnostics,
            d => d.Severity == Severity.Error && d.Variable == 0);
    }

    [Fact]
    public void TwoVariablesCantBeSavedUnderOneName()
    {
        var p = Program(new VariableDecl { Name = "points", Kind = ValueKind.Int, SaveKey = "score" });
        p.Variables.Add(new VariableDecl { Name = "score", Kind = ValueKind.String, SaveKey = "score" });
        Assert.Contains(CodeGenerator.Generate(p).Diagnostics, d => d.Severity == Severity.Error && d.Variable == 1 && d.Message.Contains("上書き"));
    }

    [Fact]
    public void ChangingASavedVariableEveryFrameWarns()
    {
        var p = new TriggerProgram();
        p.Variables.Add(new VariableDecl { Name = "steps", Kind = ValueKind.Int, SaveKey = "steps" });
        p.Events.Add(AddOne("steps"));
        p.Events[0].EventId = "Update";
        Assert.Contains(CodeGenerator.Generate(p).Diagnostics, d => d.Severity == Severity.Warning && d.Event == 0 && d.Message.Contains("保存データがすべて送られます"));
        p.Events[0].EventId = "Interact"; // now and then
        Assert.DoesNotContain(CodeGenerator.Generate(p).Diagnostics, d => d.Message.Contains("保存データがすべて送られます"));
    }
}
