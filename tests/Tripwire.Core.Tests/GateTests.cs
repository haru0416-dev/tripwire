using System.Collections.Generic;
using Tripwire.Core;
using Xunit;
using static TestKit;

/// <summary>"Who can use it": checked where the card fires, before anything is sent.</summary>
public class GateTests
{
    static TriggerProgram Click(Gate gate, List<string> names = null, Broadcast broadcast = Broadcast.Local) => new TriggerProgram
    {
        Events = { new EventBlock { EventId = "Interact", Gate = gate, GateNames = names, Broadcast = broadcast, Actions = { Log("x") } } },
    };

    [Theory]
    [InlineData(Gate.Owner, "if (Networking.IsOwner(gameObject)) _Tw_E0();")]
    [InlineData(Gate.Master, "if (Networking.IsMaster) _Tw_E0();")]
    [InlineData(Gate.InstanceOwner, "if (Networking.IsInstanceOwner) _Tw_E0();")]
    public void TheCardRunsOnlyForThosePlayers(Gate gate, string line) => Assert.Contains(line, Flat(Click(gate)));

    [Fact]
    public void AListIsCheckedByDisplayNameBeforeSending()
    {
        var src = Flat(Click(Gate.InList, new List<string> { "haru", "\"quoted\"" }, Broadcast.All));
        Assert.Contains("if ((tw_Known || Tw_Know()) && tw_In0) SendCustomNetworkEvent(NetworkEventTarget.All, \"Tw_E0\");", src);
        Assert.Contains("string[] tw_Gate0 = new string[] { \"haru\", \"\\\"quoted\\\"\" };", src);
        Assert.Contains("tw_In0 = Tw_Listed(tw_Gate0, tw_Name);", src);
        Assert.Contains("if (!((tw_Known || Tw_Know()) && tw_In0)) _Tw_E0();", Flat(Click(Gate.NotInList, new List<string> { "haru" })));
    }

    [Fact]
    public void TheNameIsLookedUpOnceAndCardsSharingAListShareTheAnswer()
    {
        var p = Click(Gate.InList, new List<string> { "haru", "alice" });
        p.Events.Add(new EventBlock { EventId = "Update", Gate = Gate.NotInList, GateNames = new List<string> { "haru", "alice" }, Actions = { Log("y") } });
        p.Events.Add(new EventBlock { EventId = "Update", Gate = Gate.InList, GateNames = new List<string> { "bob" }, Actions = { Log("z") } });
        var src = Flat(p);
        Assert.Contains("!((tw_Known || Tw_Know()) && tw_In0)", src);
        Assert.Contains("(tw_Known || Tw_Know()) && tw_In1", src);
        Assert.DoesNotContain("tw_Gate2", src);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(src, "displayName"));
        // Not worked out while there is no local player yet: asked again next time.
        Assert.Contains("if (!Utilities.IsValid(tw_P)) return false; string tw_Name = tw_P.displayName;", src);
    }

    [Fact]
    public void PlayersAClickCardIsntForCantPointAtIt()
    {
        Assert.Contains("DisableInteractive = !((tw_Known || Tw_Know()) && tw_In0);", Flat(Click(Gate.InList, new List<string> { "haru" })));
        Assert.DoesNotContain("DisableInteractive", Flat(Click(Gate.Owner))); // the owner changes during a visit
        var mixed = Click(Gate.InList, new List<string> { "haru" });
        mixed.Events.Add(new EventBlock { EventId = "Interact", Actions = { Log("y") } }); // another click card for anyone
        Assert.DoesNotContain("DisableInteractive", Flat(mixed));
    }

    [Fact]
    public void AListGateNeedsAList()
    {
        Assert.Contains(CodeGenerator.Generate(Click(Gate.InList)).Diagnostics, d => d.Severity == Severity.Error && d.Event == 0);
        Assert.Contains(CodeGenerator.Generate(Click(Gate.InList, new List<string>())).Diagnostics, d => d.Severity == Severity.Warning && d.Message.Contains("誰も使えません"));
    }

    [Fact]
    public void AGateWhereEveryScreenChecksItsOwnPlayerWarns()
    {
        // A synced variable's change runs on every screen.
        var changed = new TriggerProgram();
        changed.Variables.Add(new VariableDecl { Name = "open", Kind = ValueKind.Bool, Synced = true });
        changed.Events.Add(new EventBlock { EventId = EventCatalog.VariableChangedId, Name = "open", Gate = Gate.Master, Actions = { Log("x") } });
        Assert.Contains(CodeGenerator.Generate(changed).Diagnostics, d => d.Severity == Severity.Warning && d.Event == 0 && d.Message.Contains("画面ごとに"));
        // A Custom event this trigger sends itself to everyone.
        var sent = new TriggerProgram();
        sent.Events.Add(new EventBlock { EventId = EventCatalog.CustomId, Name = "Open", Gate = Gate.Master, Actions = { Log("x") } });
        sent.Events.Add(new EventBlock { EventId = "Interact", Actions = { new ActionCall { ActionId = ActionCatalog.SendEventId, Args = { ArgValue.SelfObject(), ArgValue.Const("Open"), ArgValue.Const(1) } } } });
        Assert.Contains(CodeGenerator.Generate(sent).Diagnostics, d => d.Severity == Severity.Warning && d.Event == 0 && d.Message.Contains("受け取った画面"));
        // Sent only to this screen: checked where it was set off, no warning.
        sent.Events[1].Actions[0].Args[2] = ArgValue.Const(0);
        Assert.DoesNotContain(CodeGenerator.Generate(sent).Diagnostics, d => d.Message.Contains("受け取った画面"));
    }
}
