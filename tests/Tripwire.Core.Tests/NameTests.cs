using Tripwire.Core;
using Xunit;

/// <summary>Names users give events: what may collide with Udon, C# or the generated code.</summary>
public class NameTests
{
    static TriggerProgram WithCustom(string name)
    {
        var p = new TriggerProgram();
        p.Events.Add(new EventBlock { EventId = "Custom", Name = name });
        return p;
    }

    [Theory]
    [InlineData("Interact")]
    [InlineData("1abc")]
    [InlineData("ドア")]
    [InlineData("class")]
    [InlineData("SendCustomEvent")]
    public void BadCustomNamesAreErrors(string name)
    {
        Assert.True(CodeGenerator.Generate(WithCustom(name)).HasErrors);
    }

    [Theory]
    [InlineData("Networking")]
    public void CustomNamesCannotShadowNamesTheGeneratedCodeUses(string name)
    {
        Assert.True(CodeGenerator.Generate(WithCustom(name)).HasErrors);
    }

    [Fact]
    public void CustomNamesCannotShadowTypeRootsOfCalls()
    {
        var p = WithCustom("Cinemachine");
        Assert.False(CodeGenerator.Generate(p).HasErrors);
        var e = new EventBlock { EventId = "Interact" };
        e.Actions.Add(new ActionCall
        {
            ActionId = ActionCatalog.CallId,
            Call = new CallSpec { UdonName = "x", DeclaringType = "Cinemachine.CinemachineCore", Member = "Foo", Kind = CallKind.Method },
        });
        p.Events.Add(e);
        Assert.True(CodeGenerator.Generate(p).HasErrors);
    }

    [Fact]
    public void UdonBuiltInEventNamesAreReservedWhenProvided()
    {
        CodeGenerator.UdonEventNames.Add("OnPlayerTriggerStay");
        try { Assert.True(CodeGenerator.Generate(WithCustom("OnPlayerTriggerStay")).HasErrors); }
        finally { CodeGenerator.UdonEventNames.Remove("OnPlayerTriggerStay"); }
    }

    [Theory]
    [InlineData("Twin", true)]
    [InlineData("Tw_Open", false)]
    [InlineData("tw_open", false)]
    [InlineData("v_x", false)]
    public void OnlyTheGeneratedCodePrefixesAreReserved(string name, bool allowed) =>
        Assert.Equal(allowed, CodeGenerator.CheckCustomEventName(name) == null);
}
