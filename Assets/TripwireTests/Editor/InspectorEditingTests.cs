using System;
using System.Collections.Generic;
using System.Linq;
using Tripwire.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tripwire.Tests
{
    /// <summary>Inspector editing: duplicate / copy / paste (deep copies keeping object references) and the list move behind dragging cards.</summary>
    public class InspectorEditingTests
    {
        [Test]
        public void DuplicatesAreIndependentAndKeepReferences()
        {
            var target = new GameObject("ClipTarget");
            try
            {
                var e = new KEvent { eventId = "Interact", name = "x" };
                e.actions.Add(new KAction { actionId = "GameObject.ToggleActive", args = { new KArg { source = KArgSource.Objects, objects = { target } } } });
                e.conditions.Add(new KCondition { variable = "v", op = KCompareOp.Greater, value = new KArg { intValue = 3 } });

                var copy = TripwireTriggerEditor.CloneOf(e);
                Assert.AreNotSame(e, copy);
                Assert.AreNotSame(e.actions[0], copy.actions[0]);
                Assert.AreSame(target, copy.actions[0].args[0].objects[0], "object references are kept");
                Assert.AreEqual(3, copy.conditions[0].value.intValue);
                copy.actions[0].args[0].objects.Clear();
                Assert.AreEqual(1, e.actions[0].args[0].objects.Count, "editing the copy leaves the original alone");

                TripwireTriggerEditor.Copy(e.actions[0]);
                Assert.IsTrue(TripwireTriggerEditor.CanPaste<KAction>());
                Assert.IsFalse(TripwireTriggerEditor.CanPaste<KEvent>(), "an action does not paste as an event");
                var pasted = TripwireTriggerEditor.Pasted<KAction>();
                Assert.AreEqual("GameObject.ToggleActive", pasted.actionId);
                Assert.AreSame(target, pasted.args[0].objects[0]);
                Assert.AreNotSame(pasted, TripwireTriggerEditor.Pasted<KAction>(), "each paste is a new copy");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        static List<string> L(params string[] xs) => new List<string>(xs);

        [TestCase("a", 3, "b,c,a")]   // to the end
        [TestCase("c", 0, "c,a,b")]   // to the front
        [TestCase("a", 2, "b,a,c")]   // after b (index counted before the move)
        public void MovesWithinAList(string item, int at, string expected)
        {
            var list = L("a", "b", "c");
            Assert.IsTrue(TripwireTriggerEditor.Move(list, item, list, at));
            Assert.AreEqual(expected, string.Join(",", list));
        }

        [TestCase(1)]
        [TestCase(2)]
        public void DroppingOnItselfChangesNothing(int at)
        {
            var list = L("a", "b", "c");
            Assert.IsFalse(TripwireTriggerEditor.Move(list, "b", list, at));
            Assert.AreEqual("a,b,c", string.Join(",", list));
        }

        [Test]
        public void MovesBetweenLists()
        {
            var from = L("a", "b");
            var to = L("x", "y");
            Assert.IsTrue(TripwireTriggerEditor.Move(from, "a", to, 1));
            Assert.AreEqual("b", string.Join(",", from));
            Assert.AreEqual("x,a,y", string.Join(",", to));
            Assert.IsTrue(TripwireTriggerEditor.Move(from, "b", to, 99)); // past the end: appended
            Assert.AreEqual("x,a,y,b", string.Join(",", to));
            Assert.IsFalse(TripwireTriggerEditor.Move(from, "zzz", to, 0), "an item no longer in its list is ignored");
        }

        [Test]
        public void ActionNumbersMatchTheGenerator()
        {
            // An action that was a block and became a Log keeps stale contents in the data: neither side may count them.
            var go = new GameObject("NumberingTrigger");
            try
            {
                var t = go.AddComponent<TripwireTrigger>();
                var stray = new KAction { actionId = "Debug.Log", args = { new KArg { stringValue = "x" } } };
                stray.thenActions.Add(new KAction { actionId = "Debug.Log", args = { new KArg { stringValue = "hidden" } } });
                var block = new KAction { actionId = Tripwire.Core.ActionCatalog.IfId };
                block.thenActions.Add(new KAction { actionId = "GameObject.ToggleActive", args = { new KArg { source = KArgSource.Objects } } });
                t.events.Add(new KEvent { eventId = "Interact", actions = { stray, block } });
                var model = TripwireModel.ToProgram(t);
                Assert.AreEqual(Tripwire.Core.ActionCall.Flatten(model.Events[0].Actions).Count, TripwireModel.FlatActions(t.events[0].actions).Count);
                Assert.AreEqual(3, TripwireModel.FlatActions(t.events[0].actions).Count, "Log, block, the block's toggle");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SavedEnumsMirrorTheGenerators()
        {
            // The component stores Core's enums as its own (Runtime can't see Core) and the model casts by number.
            void Same<TSaved, TCore>()
            {
                foreach (var name in Enum.GetNames(typeof(TSaved)))
                    Assert.AreEqual((int)Enum.Parse(typeof(TCore), name), (int)Enum.Parse(typeof(TSaved), name), typeof(TSaved).Name + "." + name);
            }
            Same<KValueType, Tripwire.Core.ValueKind>();
            Same<KBroadcast, Tripwire.Core.Broadcast>();
            Same<KPlayerFilter, Tripwire.Core.PlayerFilter>();
            Same<KCompareOp, Tripwire.Core.CompareOp>();
            Same<KArgSource, Tripwire.Core.ArgSource>();
        }

        [Test]
        public void StarterCardsGenerateWithoutErrors()
        {
            // Each starter only lacks the objects the person drags in (warnings, not errors).
            foreach (var starter in TripwireTriggerEditor.Starters)
            {
                var go = new GameObject("Starter");
                try
                {
                    var t = go.AddComponent<TripwireTrigger>();
                    starter.Make(t);
                    var g = TripwireCompiler.Generate(t);
                    Assert.IsFalse(g.HasErrors, starter.En + ": " + string.Join("; ", g.Diagnostics));
                    Assert.IsNotEmpty(t.events, starter.En);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }
        }

        [Test]
        public void RunButtonsSendTheEventsUdonEntry()
        {
            string Entry(string id, string name = "") => TripwireTriggerEditor.TryableEntry(new KEvent { eventId = id, name = name }, Tripwire.Core.EventCatalog.Get(id));
            Assert.AreEqual("_interact", Entry("Interact"));
            Assert.AreEqual("_onEnable", Entry("OnEnable"));
            Assert.AreEqual("OpenDoor", Entry("Custom", "OpenDoor"));
            Assert.IsNull(Entry("OnPlayerJoined"), "brings a player a button can't supply");
        }

        [Test]
        public void SavedActionsKeepAssetsAndDropSceneObjects()
        {
            const string dir = "Assets/TripwireTests/Temp/Saved";
            System.IO.Directory.CreateDirectory(dir);
            var material = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, dir + "/m.mat");
            var go = new GameObject("SceneThing");
            try
            {
                // A block holding an action with a scene object and an asset.
                var inner = new KAction { actionId = "Renderer.SetEnabled", args = { new KArg { source = KArgSource.Objects, objects = { go, material } }, new KArg { boolValue = true } } };
                var block = new KAction { actionId = Tripwire.Core.ActionCatalog.RepeatId, args = { new KArg { intValue = 2 }, new KArg { stringValue = "" } } };
                block.thenActions.Add(inner);
                var set = TripwireActionSet.SaveAt(block, dir + "/twice.asset");
                var loaded = TripwireActionSet.Of(TripwireActionSet.IdPrefix + AssetDatabase.AssetPathToGUID(dir + "/twice.asset"));
                Assert.IsNotNull(loaded);
                var saved = loaded.actions[0].thenActions[0];
                CollectionAssert.AreEqual(new UnityEngine.Object[] { material }, saved.args[0].objects, "the scene object is left out, the asset stays");
                Assert.AreEqual(2, inner.args[0].objects.Count, "the original keeps both");
                var t = go.AddComponent<TripwireTrigger>();
                t.events.Add(new KEvent { eventId = "Interact", actions = { TripwireTriggerEditor.CloneOf(loaded.actions[0]) } });
                Assert.IsFalse(TripwireCompiler.Generate(t).HasErrors);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                AssetDatabase.DeleteAsset(dir);
            }
        }

        [TestCase("a,c", 4, "b,d,a,c")]   // down, past the rest
        [TestCase("b,d", 0, "b,d,a,c")]   // up to the top
        [TestCase("a,b", 1, "a,b,c,d")]   // dropped on themselves
        public void SelectedCardsMoveTogetherInTheirOrder(string pick, int at, string expected)
        {
            var list = new List<object> { "a", "b", "c", "d" };
            TripwireTriggerEditor.MoveAll(list, pick.Split(',').Cast<object>().ToList(), list, at);
            Assert.AreEqual(expected, string.Join(",", list));
        }

        [Test]
        public void SelectedCardsMoveToAnotherList()
        {
            var from = new List<object> { "a", "b", "c" };
            var to = new List<object> { "x" };
            TripwireTriggerEditor.MoveAll(from, new List<object> { "a", "c" }, to, 1);
            Assert.AreEqual("b", string.Join(",", from));
            Assert.AreEqual("x,a,c", string.Join(",", to));
        }

        [Test]
        public void SeveralCopiedCardsPasteInOrder()
        {
            TripwireTriggerEditor.CopyAll(new List<KAction> { new KAction { actionId = "Debug.Log" }, new KAction { actionId = "AudioSource.Play" } });
            var pasted = TripwireTriggerEditor.PastedAll<KAction>();
            Assert.AreEqual("Debug.Log,AudioSource.Play", string.Join(",", pasted.Select(x => x.actionId)));
        }

        [Test]
        public void FixButtonsAddWhatEventsNeed()
        {
            var go = new GameObject("NeedsParts");
            try
            {
                var spec = Tripwire.Core.EventCatalog.Get("Interact");
                var fix = TripwireTriggerEditor.FixesFor(go, new KEvent { eventId = "Interact" }, spec).Single();
                fix.apply();
                Assert.IsNotNull(go.GetComponent<BoxCollider>(), fix.button);
                Assert.IsFalse(go.GetComponent<BoxCollider>().isTrigger);
                Assert.IsEmpty(TripwireTriggerEditor.FixesFor(go, new KEvent { eventId = "Interact" }, spec), "nothing left to fix");

                // An area: a trigger collider of its own; the solid one stays solid.
                var zone = Tripwire.Core.EventCatalog.Get("OnPlayerTriggerEnter");
                TripwireTriggerEditor.FixesFor(go, new KEvent { eventId = zone.Id }, zone).Single().apply();
                var boxes = go.GetComponents<BoxCollider>();
                Assert.AreEqual(2, boxes.Length);
                Assert.IsFalse(boxes[0].isTrigger);
                Assert.IsTrue(boxes[1].isTrigger);

                // A rotated object whose looks are on a child: the box still has thickness.
                var turned = new GameObject("Turned");
                turned.transform.rotation = Quaternion.Euler(0, 45, 0);
                var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
                child.transform.SetParent(turned.transform, false);
                UnityEngine.Object.DestroyImmediate(child.GetComponent<BoxCollider>());
                TripwireTriggerEditor.FixesFor(turned, new KEvent { eventId = "Interact" }, spec).Single().apply();
                var size = turned.GetComponent<BoxCollider>().size;
                UnityEngine.Object.DestroyImmediate(turned);
                Assert.Greater(Mathf.Min(size.x, size.y, size.z), 0.9f, size.ToString());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
