using System.Linq;
using NUnit.Framework;
using Tripwire.Core;
using Tripwire.Editor;
using UnityEngine;

namespace Tripwire.Tests
{
    /// <summary>Renaming a variable carries every use along (before, a renamed player variable silently became "me").</summary>
    public class VariableRenameTests
    {
        GameObject a, b;

        [TearDown] public void Clean() { Object.DestroyImmediate(a); Object.DestroyImmediate(b); }

        static KArg Var(string name) => new KArg { source = KArgSource.Variable, name = name };

        [Test]
        public void EveryUseFollowsTheNewName()
        {
            a = new GameObject("A");
            var t = a.AddComponent<TripwireTrigger>();
            t.variables.Add(new KVariable { name = "target", typeName = "VRC.SDKBase.VRCPlayerApi" });
            t.variables.Add(new KVariable { name = "count", typeName = "System.Int32" });
            t.variables.Add(new KVariable { name = "other", typeName = "System.Int32" });

            var e = new KEvent { eventId = "Interact" };
            e.conditions.Add(new KCondition { variable = "count", op = KCompareOp.Greater, value = Var("other") });
            e.actions.Add(new KAction { actionId = ActionCatalog.AddVariableId, args = { new KArg { stringValue = "count" }, new KArg { intValue = 1 } } });
            e.actions.Add(new KAction { actionId = ActionCatalog.LogId, args = { new KArg { stringValue = "{count} and { count } and {{count}} and {other}" } } });
            var iff = new KAction { actionId = "Flow.If" };
            iff.conditions.Add(new KCondition { variable = "other", op = KCompareOp.Equal, value = Var("count") });
            iff.thenActions.Add(new KAction { actionId = ActionCatalog.SetVariableId, args = { new KArg { stringValue = "other" }, Var("count") } });
            e.actions.Add(iff);
            e.actions.Add(new KAction { actionId = "Udon.Call", method = "SystemInt32.__CompareTo__SystemInt32__SystemInt32", args = { Var("other"), Var("other") }, resultVariable = "count" });
            t.events.Add(e);
            t.events.Add(new KEvent { eventId = EventCatalog.VariableChangedId, name = "count" });

            b = new GameObject("B");
            var u = b.AddComponent<TripwireTrigger>();
            var remote = new KEvent { eventId = "Interact" };
            remote.actions.Add(new KAction { actionId = ActionCatalog.SetRemoteId, args = { new KArg { source = KArgSource.Objects, objects = { a } }, new KArg { stringValue = "count" }, new KArg { intValue = 5 } } });
            u.events.Add(remote);

            Assert.IsTrue(TripwireModel.RenameVariable(t, "count", "clicks", new[] { t, u }));
            t.variables[1].name = "clicks";

            Assert.AreEqual("clicks", e.conditions[0].variable, "event condition");
            Assert.AreEqual("other", e.conditions[0].value.name, "other variables stay");
            Assert.AreEqual("clicks", e.actions[0].args[0].stringValue, "Add Variable's variable");
            Assert.AreEqual("{clicks} and {clicks} and {{count}} and {other}", e.actions[1].args[0].stringValue, "text placeholders; {{ }} stay literal");
            Assert.AreEqual("clicks", iff.conditions[0].value.name, "a value in an If condition");
            Assert.AreEqual("clicks", iff.thenActions[0].args[1].name, "a value source inside If");
            Assert.AreEqual("other", iff.thenActions[0].args[0].stringValue);
            Assert.AreEqual("clicks", t.events[1].name, "On Variable Changed");
            Assert.AreEqual("clicks", e.actions[3].resultVariable, "the variable a call's result goes into");
            Assert.AreEqual("clicks", remote.actions[0].args[1].stringValue, "another trigger's Set Another Trigger's Variable");

            // The generator agrees: the renamed trigger still compiles without errors about the variable.
            var g = CodeGenerator.Generate(TripwireModel.ToProgram(t));
            Assert.IsFalse(g.Diagnostics.Any(d => d.Severity == Severity.Error), string.Join("\n", g.Diagnostics.Select(d => d.Message)));
        }

        [Test]
        public void NothingMovesWhenNamesWouldMix()
        {
            a = new GameObject("A");
            var t = a.AddComponent<TripwireTrigger>();
            t.variables.Add(new KVariable { name = "x", typeName = "System.Int32" });
            t.variables.Add(new KVariable { name = "y", typeName = "System.Int32" });
            var e = new KEvent { eventId = "Interact" };
            e.actions.Add(new KAction { actionId = ActionCatalog.AddVariableId, args = { new KArg { stringValue = "x" }, new KArg { intValue = 1 } } });
            t.events.Add(e);
            Assert.IsFalse(TripwireModel.RenameVariable(t, "x", "y", new[] { t }), "y is taken");
            Assert.AreEqual("x", e.actions[0].args[0].stringValue);
            Assert.IsFalse(TripwireModel.RenameVariable(t, "x", "", new[] { t }));
        }

        [Test]
        public void AnEmptySingleObjectIsOneEmptySlot()
        {
            // The Inspector no longer pads the list when it draws: an empty list must mean what [null] meant.
            a = new GameObject("A");
            var t = a.AddComponent<TripwireTrigger>();
            var e = new KEvent { eventId = "Interact" };
            const string destroy = "UnityEngineObject.__Destroy__UnityEngineObject__SystemVoid"; // one object as a plain argument
            e.actions.Add(new KAction { actionId = "Udon.Call", method = destroy, args = { new KArg { source = KArgSource.Objects } } });
            t.events.Add(e);
            var withNull = new KEvent { eventId = "Interact" };
            withNull.actions.Add(new KAction { actionId = "Udon.Call", method = destroy, args = { new KArg { source = KArgSource.Objects, objects = { null } } } });
            var g1 = CodeGenerator.Generate(TripwireModel.ToProgram(t));
            t.events[0] = withNull;
            var g2 = CodeGenerator.Generate(TripwireModel.ToProgram(t));
            Assert.AreEqual(string.Join("|", g2.Diagnostics.Select(d => d.Message)), string.Join("|", g1.Diagnostics.Select(d => d.Message)));
        }

        [Test]
        public void ANameOfAnEventValueIsRefused()
        {
            // {player} in On Player Joined means the joining player; a variable named player would take it over.
            a = new GameObject("A");
            var t = a.AddComponent<TripwireTrigger>();
            t.variables.Add(new KVariable { name = "who", typeName = "System.Int32" });
            t.events.Add(new KEvent { eventId = "OnPlayerJoined" });
            Assert.IsTrue(TripwireModel.IsEventValueName(t, "player"));
            Assert.IsFalse(TripwireModel.RenameVariable(t, "who", "player", new[] { t }));
        }

        [Test]
        public void LeftoverEntriesInASingleObjectAreNotAnError()
        {
            // An argument that was an array once (the variable it sets changed type) keeps more entries than it shows.
            a = new GameObject("A");
            var t = a.AddComponent<TripwireTrigger>();
            var e = new KEvent { eventId = "Interact" };
            const string destroy = "UnityEngineObject.__Destroy__UnityEngineObject__SystemVoid";
            e.actions.Add(new KAction { actionId = "Udon.Call", method = destroy, args = { new KArg { source = KArgSource.Objects, objects = { a, a, a } } } });
            t.events.Add(e);
            var g = CodeGenerator.Generate(TripwireModel.ToProgram(t));
            Assert.IsFalse(g.Diagnostics.Any(d => d.Severity == Severity.Error), string.Join("\n", g.Diagnostics.Select(d => d.Message)));
        }

        [Test]
        public void TemplatesKeepBracesAndOtherNames()
        {
            Assert.AreEqual("{b}", TripwireModel.RenameInTemplate("{a}", "a", "b"));
            Assert.AreEqual("{{a}} {b} {ab}", TripwireModel.RenameInTemplate("{{a}} {a} {ab}", "a", "b"));
            Assert.AreEqual("open { a", TripwireModel.RenameInTemplate("open { a", "a", "b"), "an unclosed brace stays");
        }
    }
}
