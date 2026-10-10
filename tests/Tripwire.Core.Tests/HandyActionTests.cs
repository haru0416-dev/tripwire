using System.Linq;
using Tripwire.Core;
using Xunit;
using static TestKit;

/// <summary>Toggling components, Respawn, Send Random Event, Random Item, and values any text can show.</summary>
public class HandyActionTests
{
    static TriggerProgram Program(params ActionCall[] actions)
    {
        var p = new TriggerProgram();
        p.Events.Add(new EventBlock { EventId = "Interact", Actions = actions.ToList() });
        return p;
    }

    [Fact]
    public void AComponentIsFlipped()
    {
        var src = Flat(Program(new ActionCall { ActionId = "Collider.ToggleEnabled", Args = { ArgValue.Objs(1) } }));
        Assert.Contains(".enabled = !", src);
    }

    [Fact]
    public void RespawnRemembersWhereObjectsStartedAndGoesBack()
    {
        var src = Flat(Program(new ActionCall { ActionId = ActionCatalog.RespawnId, Args = { ArgValue.Objs(2) } }));
        Assert.Contains("Tw_RememberHomes();", src); // called at Start
        Assert.Contains("void Tw_RememberHomes()", src);
        Assert.Contains(".transform.position;", src);
        Assert.Contains("tw_Os.Respawn();", src); // VRC Object Sync: its own respawn, for everyone
        Assert.Contains("SetPositionAndRotation(tw_HomeP_", src);
        Assert.Contains("tw_Pk.Drop();", src);
    }

    [Fact]
    public void RespawnNeedsObjectsPlacedInTheInspector()
    {
        var p = Program(new ActionCall { ActionId = ActionCatalog.RespawnId, Args = { ArgValue.SelfObject() } });
        Assert.Contains(CodeGenerator.Generate(p).Diagnostics, d => d.Severity == Severity.Error && d.Arg == 0);
    }

    [Fact]
    public void ARandomEventIsPickedOnceAndSentToEveryTarget()
    {
        var p = Program(new ActionCall { ActionId = ActionCatalog.SendRandomEventId, Args = { ArgValue.SelfObject(), ArgValue.Const("Red\nBlue, Green"), ArgValue.Const(0) } });
        foreach (var n in new[] { "Red", "Blue", "Green" }) p.Events.Add(new EventBlock { EventId = EventCatalog.CustomId, Name = n, Actions = { Log(n) } });
        var g = CodeGenerator.Generate(p);
        Assert.False(g.HasErrors, string.Join("\n", g.Diagnostics));
        var src = Flat(p);
        Assert.Contains("new string[] { \"Red\", \"Blue\", \"Green\" }[UnityEngine.Random.Range(0, 3)]", src);
        Assert.Contains("SendCustomEvent(tw_Pick0);", src);
        var missing = CodeGenerator.Generate(Program(new ActionCall { ActionId = ActionCatalog.SendRandomEventId, Args = { ArgValue.SelfObject(), ArgValue.Const("Nope"), ArgValue.Const(0) } }));
        Assert.Contains(missing.Diagnostics, d => d.Severity == Severity.Warning && d.Message.Contains("「Nope」"));
        var empty = CodeGenerator.Generate(Program(new ActionCall { ActionId = ActionCatalog.SendRandomEventId, Args = { ArgValue.SelfObject(), ArgValue.Const(" \n"), ArgValue.Const(0) } }));
        Assert.Contains(empty.Diagnostics, d => d.Severity == Severity.Error && d.Arg == 1);
    }

    [Fact]
    public void ARandomItemGoesIntoAnObjectVariable()
    {
        var p = Program(new ActionCall { ActionId = ActionCatalog.RandomItemId, Args = { ArgValue.Const("pick"), ArgValue.Objs(3) } });
        p.Variables.Add(new VariableDecl { Name = "pick", Type = ParamType.Object("UnityEngine.GameObject") });
        var src = Flat(p);
        Assert.Contains("v_pick = tw_A0_0_list[UnityEngine.Random.Range(0, tw_A0_0_list.Length)];", src);
    }

    [Fact]
    public void AnyTextCanShowThePlayerCountYourNameAndTheTime()
    {
        var p = Program(new ActionCall { ActionId = ActionCatalog.LogId, Args = { ArgValue.Const("{プレイヤー数} 人 {自分の名前} {time}") } });
        var src = Flat(p);
        Assert.Contains("VRCPlayerApi.GetPlayerCount().ToString()", src);
        Assert.Contains("Networking.LocalPlayer.displayName", src);
        Assert.Contains("System.DateTime.Now.ToString(\"HH:mm\")", src);
        // A variable of the same name comes first.
        p.Variables.Add(new VariableDecl { Name = "time", Kind = ValueKind.Int, Initial = 3 });
        Assert.DoesNotContain("DateTime.Now", Flat(p));
    }

    [Fact]
    public void ARandomEventCallingItselfIsALoop()
    {
        var p = new TriggerProgram();
        p.Events.Add(new EventBlock { EventId = EventCatalog.CustomId, Name = "Roll", Actions = { new ActionCall { ActionId = ActionCatalog.SendRandomEventId, Args = { ArgValue.SelfObject(), ArgValue.Const("Roll\nStop"), ArgValue.Const(0) } } } });
        p.Events.Add(new EventBlock { EventId = EventCatalog.CustomId, Name = "Stop", Actions = { Log("x") } });
        Assert.Contains(CodeGenerator.Generate(p).Diagnostics, d => d.Severity == Severity.Warning && d.Message.Contains("すぐにまた自分を動かす"));
    }
}
