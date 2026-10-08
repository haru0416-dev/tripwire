using System.Linq;
using Tripwire.Core;
using Xunit;
using static TestKit;

/// <summary>Fixes from the release review: names, "this object" as a value, Send Event names, calls on synced variables.</summary>
public class GeneratorFixTests
{
    static EventBlock Custom(string name, params ActionCall[] actions)
    {
        var b = new EventBlock { EventId = EventCatalog.CustomId, Name = name };
        b.Actions.AddRange(actions);
        return b;
    }

    static ActionCall Send(ArgValue target, string name, int broadcast = 0) =>
        new ActionCall { ActionId = ActionCatalog.SendEventId, Args = { target, ArgValue.Const(name), ArgValue.Const(broadcast) } };

    static Diagnostic[] Diags(TriggerProgram p) => CodeGenerator.Generate(p).Diagnostics.ToArray();

    [Fact]
    public void ACustomEventNamedTimeWorksBesideATimer()
    {
        var p = new TriggerProgram { Events = { Custom("Time", Log("t")), new EventBlock { EventId = EventCatalog.TimerId, Name = "tick", Timer = new TimerSpec(), Actions = { Log("t") } } } };
        var src = Flat(p);
        Assert.Contains("UnityEngine.Time.time", src);
        Assert.DoesNotContain(" Time.time", src.Replace("UnityEngine.Time.time", ""));
        Assert.NotNull(CodeGenerator.CheckCustomEventName("VRCUrl"));
    }

    [Fact]
    public void ThisObjectAsAnUdonBehaviourValueIsCast()
    {
        var p = new TriggerProgram();
        p.Variables.Add(new VariableDecl { Name = "me", Kind = ValueKind.Object, Type = ParamType.Object("VRC.Udon.UdonBehaviour") });
        p.Events.Add(new EventBlock { EventId = "Interact", Actions = { new ActionCall { ActionId = ActionCatalog.SetVariableId, Args = { ArgValue.Const("me"), ArgValue.SelfObject() } } } });
        p.Events.Add(Custom("Ping", Send(ArgValue.SelfObject(), "Ping2")));
        p.Events.Add(Custom("Ping2"));
        var src = Flat(p);
        Assert.Contains("v_me = ((VRC.Udon.UdonBehaviour)(UnityEngine.Component)this);", src);
        Assert.Contains("SendCustomEvent(\"Ping2\")", src); // a call on this stays a plain call
        Assert.DoesNotContain("(UnityEngine.Component)this).SendCustomEvent", src);
    }

    [Fact]
    public void SendEventNamesThatDoNothing()
    {
        var empty = new TriggerProgram { Events = { new EventBlock { EventId = "Interact", Actions = { Send(ArgValue.SelfObject(), "  ") } } } };
        Assert.Contains(Diags(empty), d => d.Severity == Severity.Error && d.Message.Contains("名前を入れて"));

        var hidden = new TriggerProgram { Events = { new EventBlock { EventId = "Interact", Actions = { Send(ArgValue.Objs(1), "_Hidden", 1) } } } };
        Assert.Contains(Diags(hidden), d => d.Severity == Severity.Warning && d.Message.Contains("_ で始まる"));
        var local = new TriggerProgram { Events = { new EventBlock { EventId = "Interact", Actions = { Send(ArgValue.Objs(1), "_Hidden", 0) } } } };
        Assert.DoesNotContain(Diags(local), d => d.Message.Contains("_ で始まる"));

        var missing = new TriggerProgram { Events = { new EventBlock { EventId = "Interact", Actions = { Send(ArgValue.SelfObject(), "Nope") } }, Custom("Yes") } };
        Assert.Contains(Diags(missing), d => d.Severity == Severity.Warning && d.Message.Contains("「Nope」というカスタムイベントはありません"));
    }

    [Fact]
    public void ACallThatChangesASyncedStructGoesThroughItsSetter()
    {
        var p = new TriggerProgram();
        p.Variables.Add(new VariableDecl { Name = "pos", Kind = ValueKind.Vector3, Initial = new float[3], Synced = true });
        var setX = new CallSpec { UdonName = "UnityEngineVector3.__set_x__SystemSingle__SystemVoid", DeclaringType = "UnityEngine.Vector3", Member = "x", Kind = CallKind.Set,
            Instance = ParamType.Of(ValueKind.Vector3), Params = { new EventParam("value", ParamType.Of(ValueKind.Float)) } };
        p.Events.Add(new EventBlock { EventId = "Interact", Actions = { new ActionCall { ActionId = ActionCatalog.CallId, Call = setX, Args = { ArgValue.Var("pos"), ArgValue.Const(5f) } } } });
        var src = Flat(p);
        Assert.Contains("Vector3 tw_V = v_pos; tw_V.x = 5f; Tw_Set_pos(tw_V);", src);
    }

    [Fact]
    public void OnlyCallsThatChangeAVariableInPlaceAreWarned()
    {
        // A synced string's ToLower changes nothing; a synced array's SetValue changes it without the setter.
        var p = new TriggerProgram();
        p.Variables.Add(new VariableDecl { Name = "s", Kind = ValueKind.String, Initial = "", Synced = true });
        p.Variables.Add(new VariableDecl { Name = "r", Kind = ValueKind.String, Initial = "" });
        var arrayInt = ParamType.Of(ValueKind.Int); arrayInt.IsArray = true;
        p.Variables.Add(new VariableDecl { Name = "nums", Type = arrayInt, Synced = true });
        var lower = new CallSpec { UdonName = "SystemString.__ToLower__SystemString", DeclaringType = "System.String", Member = "ToLower", Kind = CallKind.Method,
            Instance = ParamType.Of(ValueKind.String), Returns = ParamType.Of(ValueKind.String) };
        var setValue = new CallSpec { UdonName = "SystemArray.__SetValue__SystemObject_SystemInt32__SystemVoid", DeclaringType = "System.Array", Member = "SetValue", Kind = CallKind.Method,
            Instance = arrayInt, Params = { new EventParam("value", ParamType.Of(ValueKind.Int)), new EventParam("index", ParamType.Of(ValueKind.Int)) } };
        p.Events.Add(new EventBlock { EventId = "Interact", Actions = {
            new ActionCall { ActionId = ActionCatalog.CallId, Call = lower, Args = { ArgValue.Var("s") }, ResultVariable = "r" },
            new ActionCall { ActionId = ActionCatalog.CallId, Call = setValue, Args = { ArgValue.Var("nums"), ArgValue.Const(1), ArgValue.Const(0) } } } });
        var warned = Diags(p).Where(d => d.Message.Contains("変化として扱われず")).ToList();
        Assert.Single(warned);
        Assert.Contains("「nums」", warned[0].Message);
    }
}

/// <summary>Odd data from the review: lone surrogates, numbers that aren't, very fast timers, temporaries, broadcast Start.</summary>
public class OddDataTests
{
    [Fact]
    public void LoneSurrogatesAreEscapedAndPairsKept()
    {
        Assert.Equal("\"\\ud83d\"", CodeGenerator.StringLiteral("\ud83d"));
        Assert.Equal("\"😀\"", CodeGenerator.StringLiteral("😀"));
    }

    [Fact]
    public void AVeryFastTimerHasASmallTolerance()
    {
        var p = new TriggerProgram { Events = { new EventBlock { EventId = EventCatalog.TimerId, Name = "t", Timer = new TimerSpec { MinSeconds = 0.01f, MaxSeconds = 0.01f }, Actions = { TestKit.Log("x") } } } };
        Assert.Contains("tw_TimerDue0 - 0.005f) return;", TestKit.Flat(p));
        var slow = new TriggerProgram { Events = { new EventBlock { EventId = EventCatalog.TimerId, Name = "t", Timer = new TimerSpec(), Actions = { TestKit.Log("x") } } } };
        Assert.Contains("tw_TimerDue0 - 0.02f) return;", TestKit.Flat(slow));
    }

    [Fact]
    public void BroadcastStartIsReported()
    {
        var p = new TriggerProgram { Events = { new EventBlock { EventId = "Start", Broadcast = Broadcast.All, Actions = { TestKit.Log("x") } } } };
        Assert.Contains(CodeGenerator.Generate(p).Diagnostics, d => d.Severity == Severity.Warning && d.Message.Contains("各プレイヤーの画面でそれぞれ起きます"));
        var local = new TriggerProgram { Events = { new EventBlock { EventId = "Start", Actions = { TestKit.Log("x") } } } };
        Assert.DoesNotContain(CodeGenerator.Generate(local).Diagnostics, d => d.Message.Contains("各プレイヤーの画面でそれぞれ起きます"));
    }
}

public class TemporaryInRegistrationTests
{
    [Fact]
    public void ATemporaryVariableCantBeARegistrationArgument()
    {
        var e = new EventBlock { EventId = "ScriptNotified", Name = "OnTv" };
        e.Listen = new ListenSpec
        {
            TargetType = "ArchiTech.ProTV.TVManager", HasTarget = true, RegisterMethod = "_RegisterListener",
            Params = { new EventParam("listener", ParamType.Objects("UdonSharp.UdonSharpBehaviour")), new EventParam("priority", ParamType.Of(ValueKind.Int)) },
            SelfIndex = 0, PassCount = 2, Args = { null, ArgValue.Var("t") }, RegistrationKey = "tv|reg",
        };
        e.Actions.Add(TestKit.Log("x"));
        var p = new TriggerProgram { Events = { e } };
        p.Variables.Add(new VariableDecl { Name = "t", Kind = ValueKind.Int, Initial = 0, Temporary = true });
        var g = CodeGenerator.Generate(p);
        Assert.Contains(g.Diagnostics, d => d.Severity == Severity.Error && d.Message.Contains("一時的な変数は登録の値には使えません"));
    }
}
