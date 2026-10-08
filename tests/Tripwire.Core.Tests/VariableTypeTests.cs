using System.Collections.Generic;
using System.Linq;
using Tripwire.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using Xunit.Abstractions;

public class VariableTypeTests
{
    readonly ITestOutputHelper output;
    public VariableTypeTests(ITestOutputHelper output) { this.output = output; }

    static VariableDecl Var(string name, ParamType type) => new VariableDecl { Name = name, Type = type };

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

    static readonly CallSpec FindGameObject = new CallSpec
    {
        UdonName = "UnityEngineGameObject.__Find__SystemString__UnityEngineGameObject",
        DeclaringType = "UnityEngine.GameObject", Member = "Find", Kind = CallKind.Method,
        Params = { new EventParam("name", ParamType.Of(ValueKind.String)) },
        Returns = ParamType.Object("UnityEngine.GameObject"),
    };

    static readonly CallSpec SetActive = new CallSpec
    {
        UdonName = "UnityEngineGameObject.__SetActive__SystemBoolean__SystemVoid",
        DeclaringType = "UnityEngine.GameObject", Member = "SetActive", Kind = CallKind.Method,
        Instance = ParamType.Objects("UnityEngine.GameObject"),
        Params = { new EventParam("value", ParamType.Of(ValueKind.Bool)) },
    };

    [Fact]
    public void ArrayVariablesAreBoundAndLoopedWithANullGuard()
    {
        // Bound whatever the Inspector holds (even nothing yet, so it shows its drop area).
        var p = new TriggerProgram();
        p.Variables.Add(Var("doors", new ParamType(ValueKind.Object, "UnityEngine.GameObject", true)));
        var e = new EventBlock { EventId = "Interact" };
        e.Actions.Add(new ActionCall { ActionId = "GameObject.ToggleActive", Args = { ArgValue.Var("doors") } });
        p.Events.Add(e);

        var g = Gen(p);
        Assert.False(g.HasErrors);
        Assert.Contains("if (v_doors != null)", g.Source); // a variable set to null must not halt the behaviour
        var b = Assert.Single(g.Bindings);
        Assert.Equal(0, b.Variable);
        Assert.True(b.IsArray);
        Assert.Equal("v_doors", b.Field);
    }

    [Fact]
    public void AnyUdonTypeViaVariablesAndConstructors()
    {
        var p = new TriggerProgram();
        p.Variables.Add(Var("scores", ParamType.OtherType("System.Int32[]")));
        p.Variables.Add(Var("best", ParamType.Of(ValueKind.Int)));
        p.Variables.Add(Var("v4", ParamType.OtherType("UnityEngine.Vector4")));
        var maxOfArray = new CallSpec
        {
            UdonName = "UnityEngineMathf.__Max__SystemInt32Array__SystemInt32",
            DeclaringType = "UnityEngine.Mathf", Member = "Max", Kind = CallKind.Method,
            Params = { new EventParam("values", ParamType.OtherType("System.Int32[]")) },
            Returns = ParamType.Of(ValueKind.Int),
        };
        var v4Ctor = new CallSpec
        {
            UdonName = "UnityEngineVector4.__ctor__SystemSingle_SystemSingle_SystemSingle_SystemSingle__UnityEngineVector4",
            DeclaringType = "UnityEngine.Vector4", Member = "ctor", Kind = CallKind.Ctor,
            Params = { new EventParam("x", ParamType.Of(ValueKind.Float)), new EventParam("y", ParamType.Of(ValueKind.Float)), new EventParam("z", ParamType.Of(ValueKind.Float)), new EventParam("w", ParamType.Of(ValueKind.Float)) },
            Returns = ParamType.OtherType("UnityEngine.Vector4"),
        };
        var e = new EventBlock { EventId = "Interact" };
        e.Actions.Add(Call(maxOfArray, "best", ArgValue.Var("scores")));
        e.Actions.Add(Call(v4Ctor, "v4", ArgValue.Const(1f), ArgValue.Var("best"), ArgValue.Const(0f), ArgValue.Const(1f)));
        p.Events.Add(e);

        Assert.False(Gen(p).HasErrors);

        // Other kinds have no literal form: a constant is an error.
        p.Events[0].Actions[0].Args[0] = ArgValue.Const(new[] { 1, 2 });
        Assert.True(CodeGenerator.Generate(p).HasErrors);
        // Constructors need somewhere to store the result.
        p.Events[0].Actions.RemoveAt(0);
        p.Events[0].Actions[0].ResultVariable = null;
        Assert.True(CodeGenerator.Generate(p).HasErrors);
    }

    [Fact]
    public void SubclassAssignmentUsesTheInstalledTypeCheck()
    {
        var p = new TriggerProgram();
        p.Variables.Add(Var("t", ParamType.Object("UnityEngine.Transform")));
        var e = new EventBlock { EventId = "Interact" };
        var destroy = new CallSpec
        {
            UdonName = "UnityEngineObject.__Destroy__UnityEngineObject__SystemVoid",
            DeclaringType = "UnityEngine.Object", Member = "Destroy", Kind = CallKind.Method,
            Params = { new EventParam("obj", ParamType.Object("UnityEngine.Component")) },
        };
        e.Actions.Add(Call(destroy, null, ArgValue.Var("t")));
        p.Events.Add(e);

        Assert.True(CodeGenerator.Generate(p).HasErrors); // default check: exact types only
        var prev = CodeGenerator.TypeAssignable;
        CodeGenerator.TypeAssignable = (want, have) => want == "UnityEngine.Component" && have == "UnityEngine.Transform";
        try
        {
            Assert.False(Gen(p).HasErrors);
        }
        finally { CodeGenerator.TypeAssignable = prev; }
    }

    [Fact]
    public void ObjectsCannotBeSyncedAndStructsWithoutEqualityAlwaysFire()
    {
        var p = new TriggerProgram();
        p.Variables.Add(new VariableDecl { Name = "go", Type = ParamType.Object("UnityEngine.GameObject"), Synced = true });
        Assert.True(CodeGenerator.Generate(p).HasErrors);

        p = new TriggerProgram();
        p.Variables.Add(new VariableDecl { Name = "r", Type = ParamType.OtherType("UnityEngine.Ray", hasEquality: false), Synced = true });
        p.Events.Add(new EventBlock { EventId = "OnVariableChanged", Name = "r", Actions = { new ActionCall { ActionId = "Debug.Log", Args = { ArgValue.Const("r") } } } });
        var g = Gen(p);
        Assert.False(g.HasErrors);
        Assert.DoesNotContain("tw_Prev_r", g.Source);
        Assert.DoesNotContain("v_r == value", g.Source);
        Assert.Contains("            _Tw_Changed_r();", g.Source.Substring(g.Source.IndexOf("OnDeserialization")));
    }
}
