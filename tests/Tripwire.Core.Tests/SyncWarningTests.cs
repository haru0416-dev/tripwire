using System.Linq;
using Tripwire.Core;
using Xunit;

/// <summary>Synced variables changed where every receiver runs the change too.</summary>
public class SyncWarningTests
{
    static ActionCall Add(string v, int n) => new ActionCall { ActionId = ActionCatalog.AddVariableId, Args = { ArgValue.Const(v), ArgValue.Const(n) } };
    static ActionCall Toggle(string v) => new ActionCall { ActionId = ActionCatalog.ToggleVariableId, Args = { ArgValue.Const(v) } };

    static TriggerProgram Counter(bool pressedSynced, bool countSynced)
    {
        var p = new TriggerProgram();
        p.Variables.Add(new VariableDecl { Name = "pressed", Kind = ValueKind.Bool, Initial = false, Synced = pressedSynced });
        p.Variables.Add(new VariableDecl { Name = "count", Kind = ValueKind.Int, Initial = 0, Synced = countSynced });
        p.Events.Add(new EventBlock { EventId = "Interact", Actions = { Toggle("pressed") } });
        p.Events.Add(new EventBlock { EventId = EventCatalog.VariableChangedId, Name = "pressed", Actions = { Add("count", 1) } });
        return p;
    }

    static Diagnostic[] Warnings(TriggerProgram p) => CodeGenerator.Generate(p).Diagnostics.Where(d => d.Severity == Severity.Warning).ToArray();

    [Fact]
    public void CountingInASyncedChangeBlockIsReported()
    {
        // "When pressed changes, add 1 to count": every receiver adds 1 too, so count grows by the number of players.
        var w = Assert.Single(Warnings(Counter(true, true)).Where(d => d.Message.Contains("受け取った全員") || d.Message.Contains("every player who receives")));
        Assert.Equal(1, w.Event);
        Assert.Equal(0, w.Action);
        Assert.Contains("count", w.Message);
    }

    [Fact]
    public void QuietWhenEitherSideIsNotSynced()
    {
        Assert.DoesNotContain(Warnings(Counter(false, true)), d => d.Message.Contains("受け取った全員") || d.Message.Contains("every player who receives"));
        Assert.DoesNotContain(Warnings(Counter(true, false)), d => d.Message.Contains("受け取った全員") || d.Message.Contains("every player who receives"));
    }
}
