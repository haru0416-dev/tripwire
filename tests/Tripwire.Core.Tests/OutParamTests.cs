using System.Linq;
using Tripwire.Core;
using Xunit;
using static TestKit;

/// <summary>Udon API calls with `out` / `ref` parameters: a variable receives each, through a local of the exact type.</summary>
public class OutParamTests
{
    // bool int.TryParse(string s, out int result)
    static readonly CallSpec TryParse = new CallSpec
    {
        UdonName = "SystemInt32.__TryParse__SystemString_SystemInt32Ref__SystemBoolean", DeclaringType = "System.Int32", Member = "TryParse", Kind = CallKind.Method,
        Params = { new EventParam("s", ParamType.Of(ValueKind.String)), new EventParam("result", ParamType.Of(ValueKind.Int), ParamPass.Out) },
        Returns = ParamType.Of(ValueKind.Bool),
    };

    // float Mathf.SmoothDamp(float current, float target, ref float currentVelocity, float smoothTime)
    static readonly CallSpec SmoothDamp = new CallSpec
    {
        UdonName = "UnityEngineMathf.__SmoothDamp__SystemSingle_SystemSingle_SystemSingleRef_SystemSingle__SystemSingle", DeclaringType = "UnityEngine.Mathf", Member = "SmoothDamp", Kind = CallKind.Method,
        Params = { new EventParam("current", ParamType.Of(ValueKind.Float)), new EventParam("target", ParamType.Of(ValueKind.Float)),
                   new EventParam("currentVelocity", ParamType.Of(ValueKind.Float), ParamPass.Ref), new EventParam("smoothTime", ParamType.Of(ValueKind.Float)) },
        Returns = ParamType.Of(ValueKind.Float),
    };

    static TriggerProgram Program(ActionCall call, params VariableDecl[] vars)
    {
        var p = new TriggerProgram();
        p.Variables.AddRange(vars);
        p.Events.Add(new EventBlock { EventId = "Interact", Actions = { call } });
        return p;
    }

    static VariableDecl Var(string name, ValueKind kind, object initial, bool synced = false) => new VariableDecl { Name = name, Kind = kind, Initial = initial, Synced = synced };

    [Fact]
    public void AnOutParameterGoesThroughALocalIntoTheVariable()
    {
        var call = new ActionCall { ActionId = ActionCatalog.CallId, Call = TryParse, Args = { ArgValue.Const("42"), ArgValue.Var("n") }, ResultVariable = "ok" };
        var src = Flat(Program(call, Var("n", ValueKind.Int, 0), Var("ok", ValueKind.Bool, false)));
        Assert.Contains("{ int tw_O1; v_ok = System.Int32.TryParse(\"42\", out tw_O1); v_n = tw_O1; }", src);
    }

    [Fact]
    public void ARefParameterStartsFromTheVariable()
    {
        var call = new ActionCall { ActionId = ActionCatalog.CallId, Call = SmoothDamp,
            Args = { ArgValue.Var("x"), ArgValue.Const(10f), ArgValue.Var("speed"), ArgValue.Const(0.3f) }, ResultVariable = "x" };
        var src = Flat(Program(call, Var("x", ValueKind.Float, 0f), Var("speed", ValueKind.Float, 0f)));
        Assert.Contains("{ float tw_O2 = v_speed; v_x = UnityEngine.Mathf.SmoothDamp(v_x, 10f, ref tw_O2, 0.3f); v_speed = tw_O2; }", src);
    }

    [Fact]
    public void ASyncedVariableReceivesThroughItsSetter()
    {
        var call = new ActionCall { ActionId = ActionCatalog.CallId, Call = TryParse, Args = { ArgValue.Const("7"), ArgValue.Var("n") } };
        var src = Flat(Program(call, Var("n", ValueKind.Int, 0, synced: true)));
        Assert.Contains("System.Int32.TryParse(\"7\", out tw_O1); Tw_Set_n(tw_O1); }", src);
    }

    [Fact]
    public void AnOutParameterNeedsAVariableOfAFittingType()
    {
        var none = CodeGenerator.Generate(Program(new ActionCall { ActionId = ActionCatalog.CallId, Call = TryParse, Args = { ArgValue.Const("1"), ArgValue.Const(0) } }));
        Assert.Contains(none.Diagnostics, d => d.Severity == Severity.Error && d.Arg == 1 && d.Message.Contains("受け取る変数"));
        var wrong = CodeGenerator.Generate(Program(new ActionCall { ActionId = ActionCatalog.CallId, Call = TryParse, Args = { ArgValue.Const("1"), ArgValue.Var("s") } },
            Var("s", ValueKind.String, "")));
        Assert.Contains(wrong.Diagnostics, d => d.Severity == Severity.Error && d.Arg == 1 && d.Message.Contains("変数「s」"));
        // An int result into a float variable: allowed exactly when the generator lets an int go into a float.
        var widen = CodeGenerator.Generate(Program(new ActionCall { ActionId = ActionCatalog.CallId, Call = TryParse, Args = { ArgValue.Const("1"), ArgValue.Var("f") } },
            Var("f", ValueKind.Float, 0f)));
        Assert.Equal(CodeGenerator.IsAssignable(ParamType.Of(ValueKind.Float), ParamType.Of(ValueKind.Int)), !widen.HasErrors);
    }

    [Fact]
    public void TheVariablesItWritesCountAsChanges()
    {
        var call = new ActionCall { ActionId = ActionCatalog.CallId, Call = TryParse, Args = { ArgValue.Const("1"), ArgValue.Var("n") }, ResultVariable = "ok" };
        Assert.Equal(new[] { "ok", "n" }, call.CallOutputs().ToArray());
        // A While whose condition reads n ends when the call writes n.
        var loop = new ActionCall { ActionId = ActionCatalog.WhileId, Conditions = { new Condition { Variable = "n", Op = CompareOp.Less, Value = ArgValue.Const(5) } }, Then = { call } };
        var g = CodeGenerator.Generate(Program(loop, Var("n", ValueKind.Int, 0), Var("ok", ValueKind.Bool, false)));
        Assert.DoesNotContain(g.Diagnostics, d => d.Message.Contains("終わらないかもしれません"));
    }

    [Fact]
    public void TheResultAndAnOutputCantShareAVariable()
    {
        var call = new ActionCall { ActionId = ActionCatalog.CallId, Call = TryParse, Args = { ArgValue.Const("1"), ArgValue.Var("n") }, ResultVariable = "n" };
        var g = CodeGenerator.Generate(Program(call, Var("n", ValueKind.Int, 0)));
        Assert.Contains(g.Diagnostics, d => d.Severity == Severity.Error && d.Message.Contains("別の変数を選んでください"));
    }

    [Fact]
    public void EveryOutputCountsForTheFrequentSyncWarning()
    {
        // The result goes into a plain variable, the out parameter into a synced one, every frame.
        var call = new ActionCall { ActionId = ActionCatalog.CallId, Call = TryParse, Args = { ArgValue.Const("1"), ArgValue.Var("n") }, ResultVariable = "ok" };
        var p = new TriggerProgram();
        p.Variables.Add(Var("n", ValueKind.Int, 0, synced: true));
        p.Variables.Add(Var("ok", ValueKind.Bool, false));
        p.Events.Add(new EventBlock { EventId = "Update", Actions = { call } });
        Assert.Contains(CodeGenerator.Generate(p).Diagnostics, d => d.Severity == Severity.Warning && d.Message.Contains("同期する変数を変えると"));
    }

    [Fact]
    public void AnArrayTheCallFillsInWarnsWhenItIsSynced()
    {
        // int LineRenderer.GetPositions(Vector3[] positions) fills the array it is given (here on a LineRenderer variable).
        var points = ParamType.OtherType("UnityEngine.Vector3[]", false);
        var getPositions = new CallSpec
        {
            UdonName = "UnityEngineLineRenderer.__GetPositions__UnityEngineVector3Array__SystemInt32", DeclaringType = "UnityEngine.LineRenderer", Member = "GetPositions", Kind = CallKind.Method,
            Instance = ParamType.Object("UnityEngine.LineRenderer"), Params = { new EventParam("positions", points, ParamPass.Fill) }, Returns = ParamType.Of(ValueKind.Int),
        };
        var call = new ActionCall { ActionId = ActionCatalog.CallId, Call = getPositions, Args = { ArgValue.Var("line"), ArgValue.Var("pts") } };
        var line = new VariableDecl { Name = "line", Type = ParamType.Object("UnityEngine.LineRenderer") };
        var g = CodeGenerator.Generate(Program(call, line, new VariableDecl { Name = "pts", Type = points, Synced = true }));
        Assert.Contains(g.Diagnostics, d => d.Severity == Severity.Warning && d.Arg == 1 && d.Message.Contains("変化として扱われず"));
        var plain = CodeGenerator.Generate(Program(call, line, new VariableDecl { Name = "pts", Type = points }));
        Assert.False(plain.HasErrors, string.Join("\n", plain.Diagnostics));
        Assert.DoesNotContain(plain.Diagnostics, d => d.Message.Contains("変化として扱われず"));
    }

    [Fact]
    public void TheMemberShowsWhichParametersAreOutputs()
    {
        Assert.Contains("out Int result", TryParse.Display());
        Assert.Contains("ref Float currentVelocity", SmoothDamp.Display());
    }
}
