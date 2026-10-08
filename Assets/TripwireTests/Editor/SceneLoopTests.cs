using System.Collections.Generic;
using System.Linq;
using Tripwire.Core;
using Tripwire.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Tripwire.Tests
{
    /// <summary>Loops through other triggers (Send Event with dragged-in targets), which only the editor can see.</summary>
    public class SceneLoopTests
    {
        readonly List<GameObject> made = new List<GameObject>();

        [TearDown]
        public void CleanUp() { foreach (var go in made) Object.DestroyImmediate(go); made.Clear(); }

        TripwireTrigger Trigger(string name)
        {
            var go = new GameObject(name);
            made.Add(go);
            return go.AddComponent<TripwireTrigger>();
        }

        static void OnCustom(TripwireTrigger t, string name, TripwireTrigger sendTo, string sendName, bool later = false, int broadcast = 0)
        {
            var send = later
                ? new KAction { actionId = ActionCatalog.SendEventDelayedId, args = { Objects(sendTo), new KArg { stringValue = sendName }, new KArg { floatValue = 1f } } }
                : new KAction { actionId = ActionCatalog.SendEventId, args = { Objects(sendTo), new KArg { stringValue = sendName }, new KArg { intValue = broadcast } } };
            t.events.Add(new KEvent { eventId = EventCatalog.CustomId, name = name, actions = { send } });
        }

        static KArg Objects(TripwireTrigger t) => new KArg { source = KArgSource.Objects, objects = { t.gameObject } };

        static Diagnostic[] Warnings(TripwireTrigger t) =>
            TripwireCompiler.Generate(t).Diagnostics.Where(d => d.Severity == Severity.Warning).ToArray();

        [Test]
        public void TwoTriggersCallingEachOtherAreReportedOnBoth()
        {
            var a = Trigger("Bell");
            var b = Trigger("Gong");
            OnCustom(a, "Ping", b, "Pong");
            OnCustom(b, "Pong", a, "Ping");
            var wa = Warnings(a).Single();
            StringAssert.Contains("Bell", wa.Message);
            StringAssert.Contains("Gong", wa.Message);
            Assert.AreEqual(0, wa.Event);
            Assert.AreEqual(0, wa.Action);
            Assert.AreEqual(1, Warnings(b).Length);
        }

        [Test]
        public void ATriggerSendingToItsOwnObjectIsReported()
        {
            var a = Trigger("Bell");
            OnCustom(a, "Ping", a, "Ping");
            Assert.AreEqual(1, Warnings(a).Length);
        }

        [Test]
        public void OneWayCallsAndDelayedCallsAreQuiet()
        {
            var a = Trigger("Bell");
            var b = Trigger("Gong");
            OnCustom(a, "Ping", b, "Pong");
            b.events.Add(new KEvent { eventId = EventCatalog.CustomId, name = "Pong" });
            Assert.IsEmpty(Warnings(a));

            var c = Trigger("Clock");
            var d = Trigger("Hand");
            OnCustom(c, "Tick", d, "Tock", later: true);
            OnCustom(d, "Tock", c, "Tick", later: true);
            Assert.IsEmpty(Warnings(c));
        }

        [Test]
        public void SendingToEveryoneInACircleIsReportedAsNetwork()
        {
            var a = Trigger("Bell");
            var b = Trigger("Gong");
            OnCustom(a, "Ping", b, "Pong", broadcast: 1);
            OnCustom(b, "Pong", a, "Ping");
            var w = Warnings(a).Single();
            Assert.IsTrue(w.Message.Contains("network") || w.Message.Contains("ネットワーク"), w.Message);
        }
    }
}
