using System.Linq;
using Tripwire.Core;
using Xunit;
using static TestKit;

/// <summary>Combinations that compile but go wrong: synced writes by calls, call-backs to this trigger, slow work every frame.</summary>
public class ComboTests
{
    static readonly ParamType Receiver = ParamType.Object("VRC.Udon.UdonBehaviour");

    static TriggerProgram Program(EventBlock e, params VariableDecl[] vars)
    {
        var p = new TriggerProgram();
        p.Variables.AddRange(vars);
        p.Events.Add(e);
        return p;
    }

    static VariableDecl Var(string name, ValueKind kind, object initial, bool synced = false, bool temporary = false) =>
        new VariableDecl { Name = name, Kind = kind, Initial = initial, Synced = synced, Temporary = temporary };

    static Diagnostic[] Diags(TriggerProgram p) => CodeGenerator.Generate(p).Diagnostics.ToArray();

    static readonly CallSpec TryParse = new CallSpec
    {
        UdonName = "SystemInt32.__TryParse__SystemString_SystemInt32Ref__SystemBoolean", DeclaringType = "System.Int32", Member = "TryParse", Kind = CallKind.Method,
        Params = { new EventParam("s", ParamType.Of(ValueKind.String)), new EventParam("result", ParamType.Of(ValueKind.Int), ParamPass.Out) }, Returns = ParamType.Of(ValueKind.Bool),
    };

    static ActionCall Parse(string into) => new ActionCall { ActionId = ActionCatalog.CallId, Call = TryParse, Args = { ArgValue.Const("1"), ArgValue.Var(into) } };

    [Fact]
    public void ACallWritingASyncedVariableGetsTheSameWarningsAsSetVariable()
    {
        var all = new EventBlock { EventId = "Interact", Broadcast = Broadcast.All, Actions = { Parse("n") } };
        Assert.Contains(Diags(Program(all, Var("n", ValueKind.Int, 0, synced: true))), d => d.Severity == Severity.Warning && d.Message.Contains("全員が同時にオーナー"));
        var received = new EventBlock { EventId = EventCatalog.DeserializationId, Actions = { Parse("n") } };
        Assert.Contains(Diags(Program(received, Var("n", ValueKind.Int, 0, synced: true))), d => d.Severity == Severity.Warning && d.Message.Contains("送受信が終わらなくなります"));
        var plain = new EventBlock { EventId = "Interact", Broadcast = Broadcast.All, Actions = { Parse("n") } };
        Assert.DoesNotContain(Diags(Program(plain, Var("n", ValueKind.Int, 0))), d => d.Message.Contains("全員が同時にオーナー"));
    }

    // int NetworkCalling.SendCustomNetworkEvent(IUdonEventReceiver, NetworkEventTarget, string, int)
    [Fact]
    public void NetworkEventsWithValuesCantComeBackToATrigger()
    {
        var send = new CallSpec
        {
            UdonName = "x", DeclaringType = "VRC.SDK3.UdonNetworkCalling.NetworkCalling", Member = "SendCustomNetworkEvent", Kind = CallKind.Method,
            Params = { new EventParam("target", Receiver), new EventParam("networkTarget", new ParamType(ValueKind.Enum, "VRC.Udon.Common.Interfaces.NetworkEventTarget")),
                       new EventParam("eventName", ParamType.Of(ValueKind.String)), new EventParam("parameter0", ParamType.Of(ValueKind.Int)) },
        };
        var call = new ActionCall { ActionId = ActionCatalog.CallId, Call = send, Args = { ArgValue.SelfObject(), ArgValue.Const("All"), ArgValue.Const("Ping"), ArgValue.Const(3) } };
        Assert.Contains(Diags(Program(new EventBlock { EventId = "Interact", Actions = { call } })), d => d.Severity == Severity.Error && d.Message.Contains("値を受け取れない"));
    }

    // VRCTweenHandle VRCTween.TweenFloat(float from, float to, float duration, IUdonEventReceiver callback, string variableName, string onUpdate)
    static readonly CallSpec TweenFloat = new CallSpec
    {
        UdonName = "y", DeclaringType = "VRC.SDK3.Components.VRCTween", Member = "TweenFloat", Kind = CallKind.Method,
        Params = { new EventParam("from", ParamType.Of(ValueKind.Float)), new EventParam("to", ParamType.Of(ValueKind.Float)), new EventParam("duration", ParamType.Of(ValueKind.Float)),
                   new EventParam("callback", Receiver), new EventParam("variableName", ParamType.Of(ValueKind.String)), new EventParam("onUpdate", ParamType.Of(ValueKind.String)) },
    };

    static ActionCall Tween(string variable, string onUpdate) => new ActionCall { ActionId = ActionCatalog.CallId, Call = TweenFloat,
        Args = { ArgValue.Const(0f), ArgValue.Const(1f), ArgValue.Const(2f), ArgValue.SelfObject(), ArgValue.Const(variable), ArgValue.Const(onUpdate) } };

    [Fact]
    public void ATweenWritesTheVariableByItsUdonName()
    {
        var p = Program(new EventBlock { EventId = "Interact", Actions = { Tween("明るさ", "") } }, Var("明るさ", ValueKind.Float, 0f));
        var src = Flat(p);
        Assert.Contains("\"" + CodeGenerator.FieldOf("明るさ") + "\"", src);
        Assert.DoesNotContain(Diags(p), d => d.Severity == Severity.Error || d.Message.Contains("変数はありません") || d.Message.Contains("直接書き込む"));
    }

    [Fact]
    public void ATweenIntoASyncedOrMissingOrTemporaryVariableIsReported()
    {
        Assert.Contains(Diags(Program(new EventBlock { EventId = "Interact", Actions = { Tween("x", "") } }, Var("x", ValueKind.Float, 0f, synced: true))),
            d => d.Severity == Severity.Warning && d.Message.Contains("直接書き込む"));
        Assert.Contains(Diags(Program(new EventBlock { EventId = "Interact", Actions = { Tween("nope", "") } })), d => d.Severity == Severity.Warning && d.Message.Contains("という変数はありません"));
        Assert.Contains(Diags(Program(new EventBlock { EventId = "Interact", Actions = { Tween("t", "") } }, Var("t", ValueKind.Float, 0f, temporary: true))),
            d => d.Severity == Severity.Error && d.Message.Contains("一時的"));
    }

    [Fact]
    public void ATweenIntoAVariableOfAnotherTypeIsAnError()
    {
        Assert.Contains(Diags(Program(new EventBlock { EventId = "Interact", Actions = { Tween("n", "") } }, Var("n", ValueKind.Int, 0))),
            d => d.Severity == Severity.Error && d.Message.Contains("変数「n」は"));
    }

    [Fact]
    public void AnUpdateCallBackToABroadcastEventIsReported()
    {
        var p = Program(new EventBlock { EventId = "Interact", Actions = { Tween("x", "Step") } }, Var("x", ValueKind.Float, 0f));
        p.Events.Add(new EventBlock { EventId = EventCatalog.CustomId, Name = "Step", Broadcast = Broadcast.All, Actions = { Log("s") } });
        Assert.Contains(Diags(p), d => d.Severity == Severity.Warning && d.Message.Contains("毎フレーム呼ばれます"));
    }

    [Fact]
    public void ACallBackToAMissingCustomEventIsReported()
    {
        var p = Program(new EventBlock { EventId = "Interact", Actions = { Tween("x", "Step") } }, Var("x", ValueKind.Float, 0f));
        Assert.Contains(Diags(p), d => d.Severity == Severity.Warning && d.Message.Contains("「Step」というカスタムイベントはありません"));
        p.Events.Add(new EventBlock { EventId = EventCatalog.CustomId, Name = "Step", Actions = { Log("s") } });
        Assert.DoesNotContain(Diags(p), d => d.Message.Contains("カスタムイベントはありません"));
    }

    [Fact]
    public void LoadingIntoATriggerWithoutTheResultEventIsReported()
    {
        var load = new CallSpec { UdonName = "z", DeclaringType = "VRC.SDK3.StringLoading.VRCStringDownloader", Member = "LoadUrl", Kind = CallKind.Method,
            Params = { new EventParam("url", ParamType.Of(ValueKind.Url)), new EventParam("udonBehaviour", Receiver) } };
        var call = new ActionCall { ActionId = ActionCatalog.CallId, Call = load, Args = { ArgValue.Var("u"), ArgValue.SelfObject() } };
        var p = Program(new EventBlock { EventId = "Interact", Actions = { call } }, new VariableDecl { Name = "u", Kind = ValueKind.Url });
        Assert.Contains(Diags(p), d => d.Severity == Severity.Warning && d.Message.Contains("そのイベントがありません"));
        p.Events.Add(new EventBlock { EventId = "OnStringLoadSuccess", Actions = { Log("ok") } });
        Assert.DoesNotContain(Diags(p), d => d.Message.Contains("そのイベントがありません"));
    }

    [Fact]
    public void GettingAComponentEveryFrameIsReported()
    {
        var body = ParamType.Object("UnityEngine.Rigidbody"); body.IsComponent = true;
        var p = Program(new EventBlock { EventId = "Update", Actions = { new ActionCall { ActionId = ActionCatalog.GetComponentId, Args = { ArgValue.Const("b"), ArgValue.SelfObject(), ArgValue.Const(0) } } } },
            new VariableDecl { Name = "b", Type = body });
        Assert.Contains(Diags(p), d => d.Severity == Severity.Warning && d.Message.Contains("コンポーネントを取り出すと"));
    }
}

/// <summary>An enum constant that isn't a member of its enum (renamed in an SDK, data from elsewhere) is refused here.</summary>
public class EnumConstantTests
{
    [Fact]
    public void AnUnknownEnumMemberIsAnError()
    {
        var old = CodeGenerator.EnumMembersOf;
        CodeGenerator.EnumMembersOf = name => name == "UnityEngine.QueryTriggerInteraction" ? new[] { "UseGlobal", "Ignore", "Collide" } : null;
        try
        {
            var raycast = new CallSpec { UdonName = "q", DeclaringType = "UnityEngine.Physics", Member = "Raycast", Kind = CallKind.Method, Returns = ParamType.Of(ValueKind.Bool),
                Params = { new EventParam("query", new ParamType(ValueKind.Enum, "UnityEngine.QueryTriggerInteraction")) } };
            TriggerProgram With(string member) => new TriggerProgram { Events = { new EventBlock { EventId = "Interact", Actions = {
                new ActionCall { ActionId = ActionCatalog.CallId, Call = raycast, Args = { ArgValue.Const(member) } } } } } };
            Assert.Contains(CodeGenerator.Generate(With("Ping")).Diagnostics, d => d.Severity == Severity.Error && d.Message.Contains("「Ping」はありません"));
            Assert.False(CodeGenerator.Generate(With("Collide")).HasErrors);
        }
        finally { CodeGenerator.EnumMembersOf = old; }
    }
}

/// <summary>A synced variable of a type Udon can't sync is refused here, not by UdonSharp for every script.</summary>
public class SyncTypeTests
{
    [Fact]
    public void AnUnsyncableTypeIsAnError()
    {
        var old = CodeGenerator.CanSync;
        CodeGenerator.CanSync = t => CodeGenerator.FullTypeName(t) != "VRC.SDK3.Components.VRCTweenHandle"; // only this type: other tests run alongside
        try
        {
            var handle = ParamType.OtherType("VRC.SDK3.Components.VRCTweenHandle", false);
            var p = new TriggerProgram { Variables = { new VariableDecl { Name = "h", Type = handle, Synced = true } }, Events = { new EventBlock { EventId = "Interact", Actions = { TestKit.Log("x") } } } };
            Assert.Contains(CodeGenerator.Generate(p).Diagnostics, d => d.Severity == Severity.Error && d.Variable == 0 && d.Message.Contains("同期できません"));
        }
        finally { CodeGenerator.CanSync = old; }
    }
}
