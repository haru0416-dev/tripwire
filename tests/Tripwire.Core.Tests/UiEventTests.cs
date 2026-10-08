using System.Linq;
using Tripwire.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

public class UiEventTests
{

    [Fact]
    public void UiEventNeedsAnElementAndLocalValues()
    {
        var p = new TriggerProgram();
        p.Events.Add(new EventBlock { EventId = "UiButtonClick" });
        // No element yet: a warning, as for an action with no objects (a fresh card isn't an error).
        var g = CodeGenerator.Generate(p);
        Assert.False(g.HasErrors);
        Assert.Contains(g.Diagnostics, d => d.Severity == Severity.Warning && d.Event == 0);

        p = new TriggerProgram();
        var e = new EventBlock { EventId = "UiSliderChanged", HasUiSource = true, Broadcast = Broadcast.All };
        e.Actions.Add(new ActionCall { ActionId = "Debug.Log", Args = { ArgValue.Param("value") } });
        p.Events.Add(e);
        Assert.True(CodeGenerator.Generate(p).HasErrors); // the slider value exists only on the presser's client
    }
}
