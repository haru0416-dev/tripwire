using System.Linq;
using Tripwire.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

public class VideoTests
{

    [Fact]
    public void UrlVariablesGetTheirInitialUrlBound()
    {
        var p = new TriggerProgram();
        p.Variables.Add(new VariableDecl { Name = "next", Type = ParamType.Of(ValueKind.Url), Initial = "https://example.com/b.mp4", Synced = true });
        var g = CodeGenerator.Generate(p);
        Assert.False(g.HasErrors, string.Join("\n", g.Diagnostics));
        Assert.Contains("[UdonSynced] public VRCUrl v_next;", g.Source);
        Assert.Equal("https://example.com/b.mp4", Assert.Single(g.Bindings).UrlValue);
    }
}
