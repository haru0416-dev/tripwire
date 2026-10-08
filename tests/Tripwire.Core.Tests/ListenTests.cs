using System.Linq;
using System.Text.RegularExpressions;
using Tripwire.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using static TestKit;

/// <summary>"Notified by another script": registration at Start, callbacks, and what is refused.</summary>
public class ListenTests
{
    static EventBlock Listen(string name, string register = "_RegisterListener", bool target = true)
    {
        var e = new EventBlock { EventId = "ScriptNotified", Name = name };
        e.Listen = new ListenSpec
        {
            TargetType = "ArchiTech.ProTV.TVManager", HasTarget = target, RegisterMethod = register,
            Params = { new EventParam("listener", ParamType.Objects("UdonSharp.UdonSharpBehaviour")), new EventParam("priority", ParamType.Of(ValueKind.Int)) },
            SelfIndex = 0, PassCount = 1, Args = { null, null }, RegistrationKey = "tv|reg",
        };
        e.Actions.Add(new ActionCall { ActionId = "Debug.Log", Args = { ArgValue.Const(name) } });
        return e;
    }

    static GeneratedProgram Gen(params EventBlock[] events)
    {
        var p = new TriggerProgram();
        p.Events.AddRange(events);
        return CodeGenerator.Generate(p);
    }

    [Fact]
    public void RegistersOnceAtStartAndCallsEveryBlockOfAName()
    {
        var g = Gen(Listen("_TvPlay"), Listen("_TvPause"), Listen("_TvPlay"));
        var src = Flat(g);
        Assert.Single(Regex.Matches(src, @"\._RegisterListener\(this\);")); // same object, method and arguments: once
        Assert.Contains("void Start() { Tw_Register(); }", src);
        Assert.Contains("public void _TvPlay() { _Tw_E0(); _Tw_E2(); }", src);
        Assert.Equal(BindingKind.ListenTarget, Assert.Single(g.Bindings).Kind); // the editor binds the notifying script here
    }

    [Fact]
    public void ExistingStartRegistersFirstAndNetworkCallsAreIgnored()
    {
        var start = new EventBlock { EventId = "Start", Actions = { new ActionCall { ActionId = "Debug.Log", Args = { ArgValue.Const("s") } } } };
        var src = Flat(Gen(start, Listen("OnUSharpVideoPlay", "RegisterCallbackReceiver")));
        Assert.Single(Regex.Matches(src, @"void Start\(\)"));
        Assert.Contains("void Start() { Tw_Register(); _Tw_E0();", src);
        // A callback name without '_' is network-callable: a network call must not run the actions.
        Assert.Contains("public void OnUSharpVideoPlay() { if (VRC.SDK3.UdonNetworkCalling.NetworkCalling.InNetworkCall) return;", src);
    }

    [Fact]
    public void RegistrationArgumentsPrepareAndUrlsArePassedInOrder()
    {
        // VideoTXL shape: _EnsureInit first, then _Register(index, this, name).
        var txl = Listen("_TxlState", "_Register");
        txl.Listen.Params.Clear();
        txl.Listen.Params.Add(new EventParam("eventIndex", ParamType.Of(ValueKind.Int)));
        txl.Listen.Params.Add(new EventParam("handler", ParamType.Objects("UnityEngine.Component")));
        txl.Listen.Params.Add(new EventParam("eventName", ParamType.Of(ValueKind.String)));
        txl.Listen.SelfIndex = 1;
        txl.Listen.PassCount = 3;
        txl.Listen.Prepare = "_EnsureInit";
        txl.Listen.Args.Clear();
        txl.Listen.Args.AddRange(new[] { ArgValue.Const(0), null, ArgValue.Const("_TxlState") });
        Assert.Contains("._EnsureInit(); tw_Listen0._Register(0, this, \"_TxlState\");", Flat(Gen(txl)));

        // Two URL arguments: each gets its own editor-filled field, in order.
        var urls = Listen("_TvPlay", "_Reg");
        urls.Listen.Params[1] = new EventParam("a", ParamType.Of(ValueKind.Url));
        urls.Listen.Params.Add(new EventParam("b", ParamType.Of(ValueKind.Url)));
        urls.Listen.PassCount = 3;
        urls.Listen.Args.Clear();
        urls.Listen.Args.AddRange(new[] { null, ArgValue.Const("https://a/"), ArgValue.Const("https://b/") });
        var g = Gen(urls);
        Flat(g);
        Assert.Equal(new[] { "https://a/", "https://b/" }, g.Bindings.Where(b => b.UrlValue != null).Select(b => b.UrlValue));
    }

    [Fact]
    public void UdonEventNamesGoToTheEventMethod()
    {
        // VizVid calls "_onVideoStart", the Udon name of OnVideoStart: it arrives there, together with an own OnVideoStart block.
        var own = new EventBlock { EventId = "OnVideoStart", Actions = { new ActionCall { ActionId = "Debug.Log", Args = { ArgValue.Const("own") } } } };
        var src = Flat(Gen(own, Listen("_onVideoStart")));
        Assert.Single(Regex.Matches(src, @"void OnVideoStart\(\)"));
        Assert.Contains("public override void OnVideoStart() { _Tw_E0(); _Tw_E1(); }", src);
        Assert.DoesNotContain("void _onVideoStart", src);
    }

    [Fact]
    public void ProblemsAreReported()
    {
        // Unusable names, Udon export names, reserved prefixes.
        foreach (var bad in new[] { "", "_", "has space", "Custom名", "_Tw_E0", "_start", "_interact", "_onVideoError", "_onPlayerJoined" })
            Assert.True(Gen(Listen(bad)).HasErrors, bad);
        Assert.True(Gen(new EventBlock { EventId = "Custom", Name = "Ping" }, Listen("Ping")).HasErrors, "clash with a Custom event");
        Assert.True(Gen(Listen("_TvPlay", target: false)).HasErrors, "registration without a target");
        var other = Listen("_TvPlay");
        other.Listen.RegistrationKey = "tvB|reg";
        Assert.True(Gen(Listen("_TvPlay"), other).HasErrors, "one name from two senders");

        var broadcast = Listen("_TvPlay");
        broadcast.Broadcast = Broadcast.All;
        var g = Gen(broadcast);
        Assert.False(g.HasErrors);
        Assert.Contains(g.Diagnostics, d => d.Severity == Severity.Warning && d.Event == 0);

        // No registration (the user lists the trigger in the script's Inspector): fine, nothing registered.
        Assert.DoesNotContain("Tw_Register", Flat(Gen(Listen("_TvPlay", register: null, target: false))));
    }
}
