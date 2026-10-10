using System.Linq;
using Tripwire.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using Xunit.Abstractions;
using static TestKit;

public class GeneratorTests
{
    readonly ITestOutputHelper output;
    public GeneratorTests(ITestOutputHelper output) { this.output = output; }

    static ActionCall Action(string id, params ArgValue[] args)
    {
        var a = new ActionCall { ActionId = id };
        a.Args.AddRange(args);
        return a;
    }

    static EventBlock Event(string id, params ActionCall[] actions)
    {
        var e = new EventBlock { EventId = id };
        e.Actions.AddRange(actions);
        return e;
    }

    GeneratedProgram Gen(TriggerProgram p)
    {
        var g = CodeGenerator.Generate(p);
        output.WriteLine(g.Source);
        foreach (var d in g.Diagnostics) output.WriteLine(d.ToString());
        return g;
    }

    [Theory]
    [InlineData(1)] // one object: a plain field, no loop
    [InlineData(2)]
    public void InteractTargetsAreBoundFieldsOfTheRightShape(int objects)
    {
        var p = new TriggerProgram();
        var e = Event("Interact", Action("GameObject.ToggleActive", ArgValue.Objs(objects)));
        e.InteractText = "Light";
        p.Events.Add(e);

        var g = Gen(p);
        Assert.False(g.HasErrors);
        AssertParses(g);
        Assert.Equal("NoVariableSync", g.SyncMode);
        Assert.Equal("Light", g.InteractText);
        // The editor fills the field from the binding: their shapes must agree.
        var b = Assert.Single(g.Bindings);
        Assert.Equal(objects > 1, b.IsArray);
        Assert.Equal("UnityEngine.GameObject", b.UnityType);
        Assert.Contains("public UnityEngine.GameObject" + (b.IsArray ? "[] " : " ") + b.Field + ";", g.Source);
    }

    [Fact]
    public void ClassNameIsStableAndContentAddressed()
    {
        TriggerProgram Make(bool active)
        {
            var p = new TriggerProgram();
            p.Events.Add(Event("Interact", Action("GameObject.SetActive", ArgValue.Objs(1), ArgValue.Const(active))));
            return p;
        }
        var a1 = CodeGenerator.Generate(Make(true));
        var a2 = CodeGenerator.Generate(Make(true));
        var b = CodeGenerator.Generate(Make(false));
        Assert.Equal(a1.ClassName, a2.ClassName);
        Assert.NotEqual(a1.ClassName, b.ClassName);
        Assert.Matches("^Tripwire_[0-9a-f]{16}$", a1.ClassName);
        Assert.Contains("public class " + a1.ClassName + " : UdonSharpBehaviour", a1.Source);
    }

    [Fact]
    public void SyncedToggleReachesLateJoiners()
    {
        var p = new TriggerProgram();
        p.Variables.Add(new VariableDecl { Name = "isOpen", Kind = ValueKind.Bool, Synced = true });
        p.Events.Add(Event("Interact", Action("Variable.Toggle", ArgValue.Const("isOpen"))));
        var changed = Event("OnVariableChanged", Action("GameObject.SetActive", ArgValue.Objs(1), ArgValue.Var("isOpen")));
        changed.Name = "isOpen";
        p.Events.Add(changed);

        var g = Gen(p);
        Assert.False(g.HasErrors);
        AssertParses(g);
        Assert.Equal("Manual", g.SyncMode);
        Assert.Contains("[UdonSynced] public bool v_isOpen", g.Source);
        Assert.Contains("RequestSerialization();", g.Source);
        Assert.Contains("public override void OnDeserialization()", g.Source);
        // OnDeserialization fires the change handler, which runs event 1.
        var changedBody = g.Source.Substring(g.Source.IndexOf("void _Tw_Changed_isOpen()"));
        Assert.Contains("Tw_E1();", changedBody.Substring(0, changedBody.IndexOf('}')));
    }

    [Fact]
    public void PlayerTriggerFilterDefaultsCanBeLocal()
    {
        var p = new TriggerProgram();
        var e = Event("OnPlayerTriggerEnter", Action("Player.Teleport", ArgValue.Objs(1)));
        e.PlayerFilter = PlayerFilter.LocalPlayer;
        p.Events.Add(e);

        var g = Gen(p);
        Assert.False(g.HasErrors);
        AssertParses(g);
        Assert.Contains("if (Utilities.IsValid(player) && player.isLocal) _Tw_E0();", g.Source); // not everyone's teleport
    }

    [Fact]
    public void EventParamsRejectedForBroadcast()
    {
        var p = new TriggerProgram();
        var e = Event("OnTriggerEnter", Action("GameObject.SetActive", ArgValue.Param("other"), ArgValue.Const(false)));
        e.Broadcast = Broadcast.All;
        p.Events.Add(e);

        var g = Gen(p);
        Assert.True(g.HasErrors);
        Assert.Contains(g.Diagnostics, d => d.Event == 0 && d.Action == 0 && d.Arg == 0);
    }

    [Fact]
    public void LocalBodiesAreNotNetworkCallable()
    {
        var p = new TriggerProgram();
        p.Events.Add(new EventBlock { EventId = "Interact", Actions = { new ActionCall { ActionId = "Debug.Log", Args = { ArgValue.Const("x") } } } });
        p.Events.Add(new EventBlock { EventId = "Start", Broadcast = Broadcast.All, Actions = { new ActionCall { ActionId = "Debug.Log", Args = { ArgValue.Const("y") } } } });
        var g = CodeGenerator.Generate(p);
        Assert.False(g.HasErrors);
        Assert.Contains("        void _Tw_E0()", g.Source);
        Assert.DoesNotContain("public void _Tw_E0()", g.Source);
        Assert.Contains("public void Tw_E1()", g.Source);
        Assert.DoesNotContain("nameof(", g.Source);
    }

    [Fact]
    public void DelayedPlayerParamWarns()
    {
        var p = new TriggerProgram();
        var e = new EventBlock { EventId = "OnPlayerTriggerEnter", DelaySeconds = 2f };
        e.Actions.Add(new ActionCall
        {
            ActionId = ActionCatalog.CallId,
            Call = new CallSpec
            {
                UdonName = "VRCSDKBaseVRCPlayerApi.__SetWalkSpeed__SystemSingle__SystemVoid",
                DeclaringType = "VRC.SDKBase.VRCPlayerApi", Member = "SetWalkSpeed", Kind = CallKind.Method,
                Instance = ParamType.Of(ValueKind.Player),
                Params = { new EventParam("speed", ParamType.Of(ValueKind.Float)) },
            },
            Args = { ArgValue.Param("player"), ArgValue.Const(1f) },
        });
        p.Events.Add(e);
        var g = CodeGenerator.Generate(p);
        Assert.False(g.HasErrors);
        Assert.Contains(g.Diagnostics, d => d.Severity == Severity.Warning && d.Action == 0);
    }

    [Fact]
    public void StringsAreEscaped()
    {
        Assert.Equal("\"a\\\"b\\\\c\\nd\"", CodeGenerator.StringLiteral("a\"b\\c\nd"));
        Assert.Equal("\"\\u2028\"", CodeGenerator.StringLiteral("\u2028"));
        Assert.Equal("\"日本語\"", CodeGenerator.StringLiteral("日本語"));
        Assert.Equal("\"a\\u0085b\"", CodeGenerator.StringLiteral("a\u0085b"));
        Assert.Equal("\"\\u009f\"", CodeGenerator.StringLiteral("\u009f"));
    }

    [Fact]
    public void FloatsUseInvariantCulture()
    {
        var prev = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            Assert.Equal("1.5f", CodeGenerator.Literal(ValueKind.Float, 1.5f));
            Assert.Equal("new Vector3(1f, -2.25f, 0f)", CodeGenerator.Literal(ValueKind.Vector3, new[] { 1f, -2.25f, 0f }));
        }
        finally { System.Threading.Thread.CurrentThread.CurrentCulture = prev; }
    }

    [Fact]
    public void EveryCatalogActionGeneratesWithDefaults()
    {
        // Fill every action with its defaults (objects: one assigned) and make sure it generates and parses.
        var p = new TriggerProgram();
        p.Variables.Add(new VariableDecl { Name = "b", Kind = ValueKind.Bool });
        p.Variables.Add(new VariableDecl { Name = "n", Kind = ValueKind.Int });
        var body = ParamType.Object("UnityEngine.Rigidbody"); body.IsComponent = true;
        p.Variables.Add(new VariableDecl { Name = "body", Type = body }); // Get Component
        p.Variables.Add(new VariableDecl { Name = "go", Type = ParamType.Object("UnityEngine.GameObject") }); // Random Item
        var e = Event("Interact");
        p.Events.Add(new EventBlock { EventId = "Timer", Name = "tick", Timer = new TimerSpec() }); // for Start / Stop Timer
        foreach (var spec in ActionCatalog.All)
        {
            if (spec.Special == ActionSpecial.Call) continue; // needs a chosen member; see UdonCallTests
            if (spec.HoldsActions || spec.Special == ActionSpecial.Break || spec.Special == ActionSpecial.Continue) continue; // blocks and loop-only actions: see BranchTests, LoopTests
            if (spec.Special == ActionSpecial.SetRemoteVariable || spec.Special == ActionSpecial.GetRemoteVariable) continue; // need the other trigger: see RemoteTests
            var call = new ActionCall { ActionId = spec.Id };
            foreach (var prm in spec.Params)
            {
                bool number = spec.Special == ActionSpecial.AddVariable || spec.Special == ActionSpecial.RandomVariable || spec.Special == ActionSpecial.Calculate;
                if (prm.VariableRef) call.Args.Add(ArgValue.Const(spec.Special == ActionSpecial.GetComponent ? "body" : spec.Special == ActionSpecial.RandomItem ? "go" : number ? "n" : "b"));
                else if (prm.Name == "event" || prm.Name == "events") call.Args.Add(ArgValue.Const("Ping")); // Send Event: an empty name is an error now
                else if (prm.TimerRef) call.Args.Add(ArgValue.Const("tick"));
                else if (prm.Type == null) call.Args.Add(ArgValue.Const(number ? (object)1 : true));
                else if (prm.Type.Kind == ValueKind.Object) call.Args.Add(ArgValue.Objs(1));
                else if (prm.Type.Kind == ValueKind.Player) call.Args.Add(ArgValue.Local());
                else call.Args.Add(ArgValue.Const(prm.Default));
            }
            e.Actions.Add(call);
        }
        p.Events.Add(e);

        var g = Gen(p);
        Assert.False(g.HasErrors, string.Join("\n", g.Diagnostics));
        AssertParses(g);
    }
}

public class CodeShapeTests
{
    static TriggerProgram WithCondition(ValueKind kind, CompareOp op, object constant)
    {
        var p = new TriggerProgram();
        p.Variables.Add(new VariableDecl { Name = "v", Kind = kind, Initial = constant });
        var e = new EventBlock { EventId = "Interact" };
        e.Actions.Add(new ActionCall { ActionId = "Debug.Log", Args = { ArgValue.Const("x") } });
        e.Conditions.Add(new Condition { Variable = "v", Op = op, Value = ArgValue.Const(constant) });
        p.Events.Add(e);
        return p;
    }

    [Theory]
    [InlineData(CompareOp.Equal, true, "if (!v_v) return;")]
    [InlineData(CompareOp.Equal, false, "if (v_v) return;")]
    [InlineData(CompareOp.NotEqual, true, "if (v_v) return;")]
    public void BoolConditionsTestTheVariableItself(CompareOp op, bool constant, string expected) =>
        Assert.Contains(expected, WithoutTrace(CodeGenerator.Generate(WithCondition(ValueKind.Bool, op, constant)).Source));

    [Theory]
    [InlineData(CompareOp.Less, "if (v_v >= 3) return;")]
    [InlineData(CompareOp.LessOrEqual, "if (v_v > 3) return;")]
    [InlineData(CompareOp.Greater, "if (v_v <= 3) return;")]
    [InlineData(CompareOp.GreaterOrEqual, "if (v_v < 3) return;")]
    [InlineData(CompareOp.Equal, "if (v_v != 3) return;")]
    [InlineData(CompareOp.NotEqual, "if (v_v == 3) return;")]
    public void IntConditionsAreInverted(CompareOp op, string expected) =>
        Assert.Contains(expected, WithoutTrace(CodeGenerator.Generate(WithCondition(ValueKind.Int, op, 3)).Source));

    [Fact]
    public void FloatConditionsKeepTheNegation() // NaN: !(x > 0.5f) differs from x <= 0.5f
        => Assert.Contains("if (!(v_v > 0.5f)) return;", WithoutTrace(CodeGenerator.Generate(WithCondition(ValueKind.Float, CompareOp.Greater, 0.5f)).Source));

    [Fact]
    public void SeveralConditionsFailOnAnyOne()
    {
        var p = WithCondition(ValueKind.Int, CompareOp.Greater, 1);
        p.Variables.Add(new VariableDecl { Name = "on", Kind = ValueKind.Bool, Initial = false });
        p.Events[0].Conditions.Add(new Condition { Variable = "on", Op = CompareOp.Equal, Value = ArgValue.Const(true) });
        Assert.Contains("if (v_v <= 1 || !v_on) return;", WithoutTrace(CodeGenerator.Generate(p).Source));
    }

}
