using System.Linq;
using NUnit.Framework;
using Tripwire.Core;
using Tripwire.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tripwire.Tests
{
    /// <summary>Saved data stays safe: newer data isn't touched, folding isn't an edit, unknown actions can be removed, uses are counted.</summary>
    public class DataSafetyTests
    {
        GameObject a, b;

        [TearDown] public void Clean() { Object.DestroyImmediate(a); Object.DestroyImmediate(b); }

        TripwireTrigger Make(string name, out GameObject go)
        {
            go = new GameObject(name);
            var t = go.AddComponent<TripwireTrigger>();
            t.variables.Add(new KVariable { name = "count", typeName = "System.Int32" });
            var e = new KEvent { eventId = "Interact" };
            e.conditions.Add(new KCondition { variable = "count", op = KCompareOp.Greater, value = new KArg { intValue = 1 } });
            e.actions.Add(new KAction { actionId = ActionCatalog.AddVariableId, args = { new KArg { stringValue = "count" }, new KArg { intValue = 1 } } });
            e.actions.Add(new KAction { actionId = ActionCatalog.LogId, args = { new KArg { stringValue = "{count}" } } });
            t.events.Add(e);
            return t;
        }

        [Test]
        public void UsesAreCountedAcrossTriggers()
        {
            var t = Make("A", out a);
            b = new GameObject("B");
            var u = b.AddComponent<TripwireTrigger>();
            var remote = new KEvent { eventId = "Interact" };
            remote.actions.Add(new KAction { actionId = ActionCatalog.SetRemoteId, args = { new KArg { source = KArgSource.Objects, objects = { a } }, new KArg { stringValue = "count" }, new KArg { intValue = 5 } } });
            u.events.Add(remote);
            // The condition, Add Variable, {count} in the log, and the other trigger's remote set.
            Assert.AreEqual(4, TripwireModel.CountVariableUses(t, "count", new[] { t, u }));
            Assert.AreEqual(0, TripwireModel.CountVariableUses(t, "other", new[] { t, u }));
            Assert.AreEqual("count", t.events[0].conditions[0].variable, "counting changes nothing");
        }

        [Test]
        public void DataFromANewerVersionIsNeitherAppliedNorEdited()
        {
            LogAssert.ignoreFailingMessages = true;
            var t = Make("A", out a);
            t.dataVersion = TripwireTrigger.CurrentDataVersion + 1;
            Assert.IsTrue(TripwireMigration.IsNewer(t));
            Assert.AreEqual(TripwireCompiler.State.ApplyFailed, TripwireCompiler.ApplyAll(new[] { t }));
            StringAssert.Contains(TripwireMigration.NewerMessage, TripwireCompiler.FailureOf(t));
            Assert.IsNull(t.generated, "nothing was attached");
            Assert.AreEqual(TripwireTrigger.CurrentDataVersion + 1, t.dataVersion, "left as saved");
        }

        [Test]
        public void OlderDataIsReadWithoutMakingTheSceneUnsaved()
        {
            var t = Make("A", out a);
            t.dataVersion = 0;
            int dirty = EditorUtility.GetDirtyCount(t);
            Assert.IsFalse(TripwireMigration.Upgrade(t), "version 0 is the current format");
            Assert.AreEqual(dirty, EditorUtility.GetDirtyCount(t));
        }

        [Test]
        public void FoldingIsNotAnEdit()
        {
            var t = Make("A", out a);
            var folded = new KEvent { eventId = "Interact", expanded = false };
            t.events.Add(folded);
            TripwireFolds.EnsureIds(t);
            Assert.IsFalse(TripwireFolds.IsExpanded(folded), "a card folded in older data stays folded");
            Assert.IsTrue(TripwireFolds.IsExpanded(t.events[0]));

            int dirty = EditorUtility.GetDirtyCount(t);
            TripwireFolds.SetExpanded(t.events[0], false);
            Assert.IsFalse(TripwireFolds.IsExpanded(t.events[0]));
            Assert.AreEqual(dirty, EditorUtility.GetDirtyCount(t), "no unsaved scene from folding");
            Assert.IsTrue(t.events[0].expanded, "the scene data doesn't change");

            var copy = TripwireTriggerEditor.CloneOf(t.events[0]);
            Assert.AreEqual("", copy.id, "a copy is another card");
            TripwireFolds.SetExpanded(t.events[0], true);
        }

        [Test]
        public void ASceneWithoutVRCWorldIsPointedOutAndFixed()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            Make("A", out a);
            Assert.IsTrue(TripwireSetup.MissingWorld(scene), "no VRCWorld: ClientSim wouldn't start");
            TripwireSetup.AddWorld(scene);
            Assert.IsFalse(TripwireSetup.MissingWorld(scene));
            Assert.IsNotNull(Object.FindObjectOfType<VRC.SDKBase.VRC_SceneDescriptor>(), "the SDK's VRCWorld is in the scene");
        }

        [Test]
        public void EveryStarterOnlyWaitsForItsObjects()
        {
            var before = Texts.Language;
            Texts.Language = UiLanguage.English;
            var lists = InspectorEditingTests.PlayerLists();
            try
            {
                foreach (var starter in TripwireTriggerEditor.Starters)
                {
                    Object.DestroyImmediate(a);
                    a = new GameObject(starter.En);
                    var t = a.AddComponent<TripwireTrigger>();
                    starter.Make(t);
                    var errors = TripwireCompiler.Generate(t).Diagnostics.Where(d => d.Severity == Severity.Error).Select(d => d.Message).ToList();
                    // What is left is dragging in the objects (or the UI element); nothing else to fix.
                    Assert.IsTrue(errors.All(m => m.Contains("UI element") || m.Contains("object")), starter.En + ": " + string.Join(" | ", errors));
                }
            }
            finally { Texts.Language = before; InspectorEditingTests.RemovePlayerListsSince(lists); }
        }

        [Test]
        public void UnknownActionsAreReportedAndCanBeRemoved()
        {
            var t = Make("A", out a);
            var iff = new KAction { actionId = ActionCatalog.IfId };
            iff.thenActions.Add(new KAction { actionId = "Gone.FromAnAddOn" });
            t.events[0].actions.Add(iff);
            t.events[0].actions.Add(new KAction { actionId = "Gone.FromAnAddOn" });
            var g = TripwireCompiler.Generate(t);
            Assert.IsTrue(g.Diagnostics.Any(d => d.Severity == Severity.Error && d.Message.Contains("Gone.FromAnAddOn")), "named in the error");

            var remove = typeof(TripwireTriggerEditor).GetMethod("RemoveUnknown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            foreach (var e in t.events) remove.Invoke(null, new object[] { e.actions });
            Assert.AreEqual(0, iff.thenActions.Count, "removed from inside blocks too");
            Assert.AreEqual(3, t.events[0].actions.Count, "known actions stay (the If, now empty, included)");
        }
    }
}
