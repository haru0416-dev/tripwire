using System.Collections.Generic;
using System.Linq;
using Tripwire.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using Xunit.Abstractions;

public class UdonCallTests
{
    readonly ITestOutputHelper output;
    public UdonCallTests(ITestOutputHelper output) { this.output = output; }

    static CallSpec Rotate() => new CallSpec
    {
        UdonName = "UnityEngineTransform.__Rotate__UnityEngineVector3_UnityEngineSpace__SystemVoid",
        DeclaringType = "UnityEngine.Transform",
        Member = "Rotate",
        Kind = CallKind.Method,
        Instance = ParamType.Objects("UnityEngine.Transform"),
        Params = new List<EventParam>
        {
            new EventParam("eulers", ParamType.Of(ValueKind.Vector3)),
            new EventParam("relativeTo", new ParamType(ValueKind.Enum, "UnityEngine.Space")),
        },
    };

    static CallSpec MathfMax() => new CallSpec
    {
        UdonName = "UnityEngineMathf.__Max__SystemInt32_SystemInt32__SystemInt32",
        DeclaringType = "UnityEngine.Mathf",
        Member = "Max",
        Kind = CallKind.Method,
        Params = new List<EventParam> { new EventParam("a", ParamType.Of(ValueKind.Int)), new EventParam("b", ParamType.Of(ValueKind.Int)) },
        Returns = ParamType.Of(ValueKind.Int),
    };

    static CallSpec PlayerName() => new CallSpec
    {
        UdonName = "VRCSDKBaseVRCPlayerApi.__get_displayName__SystemString",
        DeclaringType = "VRC.SDKBase.VRCPlayerApi",
        Member = "displayName",
        Kind = CallKind.Get,
        Instance = ParamType.Of(ValueKind.Player),
        Returns = ParamType.Of(ValueKind.String),
    };

    static ActionCall Call(CallSpec c, string result, params ArgValue[] args)
    {
        var a = new ActionCall { ActionId = ActionCatalog.CallId, Call = c, ResultVariable = result };
        a.Args.AddRange(args);
        return a;
    }

    GeneratedProgram Gen(TriggerProgram p)
    {
        var g = CodeGenerator.Generate(p);
        output.WriteLine(g.Source);
        foreach (var d in g.Diagnostics) output.WriteLine(d.ToString());
        var tree = CSharpSyntaxTree.ParseText(g.Source, new CSharpParseOptions(LanguageVersion.CSharp9));
        Assert.DoesNotContain(tree.GetDiagnostics(), d => d.Severity == DiagnosticSeverity.Error);
        return g;
    }

    [Fact]
    public void PropertyGetOnPlayerNeedsAVariable()
    {
        var p = new TriggerProgram();
        p.Variables.Add(new VariableDecl { Name = "who", Kind = ValueKind.String });
        var e = new EventBlock { EventId = "OnPlayerJoined" };
        e.Actions.Add(Call(PlayerName(), "who", ArgValue.Param("player")));
        p.Events.Add(e);

        Assert.False(Gen(p).HasErrors);
        p.Events[0].Actions[0].ResultVariable = null; // a getter whose result goes nowhere does nothing
        Assert.True(CodeGenerator.Generate(p).HasErrors);
    }

    [Theory]
    [InlineData("World; Debug.Log(1)")]
    [InlineData("")]
    public void EnumMembersMustBeIdentifiers(string member)
    {
        var p = new TriggerProgram();
        var e = new EventBlock { EventId = "Interact" };
        e.Actions.Add(Call(Rotate(), null, ArgValue.Objs(1), ArgValue.Const(new[] { 0f, 0f, 0f }), ArgValue.Const(member)));
        p.Events.Add(e);
        Assert.True(CodeGenerator.Generate(p).HasErrors);
    }

    [Fact]
    public void ResultTypeMismatchIsAnError()
    {
        var p = new TriggerProgram();
        p.Variables.Add(new VariableDecl { Name = "flag", Kind = ValueKind.Bool });
        var e = new EventBlock { EventId = "Interact" };
        e.Actions.Add(Call(MathfMax(), "flag", ArgValue.Const(1), ArgValue.Const(2)));
        p.Events.Add(e);
        Assert.True(CodeGenerator.Generate(p).HasErrors);
    }
}

public class UnityMathTests
{
    [Theory]
    [InlineData(0f, 90f, 0f, 0f, 0.70710677f, 0f, 0.70710677f)]
    [InlineData(90f, 0f, 0f, 0.70710677f, 0f, 0f, 0.70710677f)]
    [InlineData(0f, 0f, 90f, 0f, 0f, 0.70710677f, 0.70710677f)]
    public void EulerMatchesKnownValues(float x, float y, float z, float qx, float qy, float qz, float qw)
    {
        var q = UnityMath.Euler(x, y, z);
        Assert.Equal(qx, q[0], 5);
        Assert.Equal(qy, q[1], 5);
        Assert.Equal(qz, q[2], 5);
        Assert.Equal(qw, q[3], 5);
    }

    [Fact]
    public void QuaternionConstantsAreFoldableCtors()
    {
        var lit = CodeGenerator.Literal(ValueKind.Quaternion, new[] { 0f, 90f, 0f });
        Assert.StartsWith("new Quaternion(", lit);
        Assert.DoesNotContain("Euler", lit);
    }
}
