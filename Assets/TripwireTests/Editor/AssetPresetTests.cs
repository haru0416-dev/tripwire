using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tripwire.Core;
using Tripwire.Editor;
using NUnit.Framework;
using UdonSharp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VRC.Udon;
using Object = UnityEngine.Object;

using static Tripwire.Tests.TestSupport;

namespace Tripwire.Tests
{
    /// <summary>
    /// Asset presets against the real assets (installed by scripts/fetch-test-assets.sh; ignored when absent):
    /// every preset entry exists in the installed version, the generated code compiles against it, and the trigger
    /// ends up in each asset's own listener list.
    /// </summary>
    public class AssetPresetTests : PlayModeTest
    {
        const string SceneDir = "Assets/TripwireTests/Temp";
        const string ScenePath = SceneDir + "/AssetPresets.unity";

        // Prefab per preset, and where each asset keeps its listeners (its own field names).
        static readonly (string preset, string prefab, string listeners)[] Assets =
        {
            ("protv", "Packages/dev.architech.protv/Simple (ProTV).prefab", "_eventListeners"),
            ("vizvid", "Packages/idv.jlchntoz.vvmw/VVMW (No Controls).prefab", "targets"),
            ("usharpvideo", "Assets/TripwireTestAssets/USharpVideo/USharpVideo.prefab", "_registeredCallbackReceivers"),
            ("videotxl", "Packages/com.texelsaur.video/Runtime/Prefabs/Sync Video Player.prefab", "handlers"),
        };

        static IEnumerable<AssetPresets.Preset> Installed() => AssetPresets.All.Where(p => TripwireModel.ResolveType(p.ScriptType) != null);

        /// <summary>The concrete script class a preset meets in its prefab.</summary>
        static Component ScriptIn(GameObject root, AssetPresets.Preset p) =>
            root.GetComponentsInChildren<UdonSharpBehaviour>(true).FirstOrDefault(b => TripwireModel.ResolveType(p.ScriptType).IsAssignableFrom(b.GetType()));

        [Test]
        public void PresetEntriesExistInTheInstalledVersions()
        {
            if (!Installed().Any()) Assert.Ignore("No preset assets installed (scripts/fetch-test-assets.sh).");
            var missing = new List<string>();
            foreach (var (id, prefabPath, _) in Assets)
            {
                var p = AssetPresets.Get(id);
                if (TripwireModel.ResolveType(p.ScriptType) == null) continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                Assert.IsNotNull(prefab, prefabPath);
                var script = ScriptIn(prefab, p).GetType();
                foreach (var n in p.Notifications)
                    if (PresetResolver.RegistrationFor(script, n) == null) missing.Add(id + " notification " + n.Callback + " (" + n.RegisterMethod + ")");
                foreach (var op in p.Operations)
                    if (PresetResolver.Resolve(script, op) == null) missing.Add(id + " operation " + op.En + " (" + string.Join(", ", op.Steps.Select(s => s.Member)) + ")");
            }
            // VizVid's FrontendHandler lives in the same prefab as its Core.
            var ui = AssetPresets.Get("vizvid-frontend");
            var vvmw = AssetDatabase.LoadAssetAtPath<GameObject>(Assets.First(a => a.preset == "vizvid").prefab);
            if (vvmw != null)
            {
                var script = ScriptIn(vvmw, ui).GetType();
                foreach (var n in ui.Notifications)
                    if (PresetResolver.RegistrationFor(script, n) == null) missing.Add("vizvid-frontend notification " + n.Callback);
                foreach (var op in ui.Operations)
                    if (PresetResolver.Resolve(script, op) == null) missing.Add("vizvid-frontend operation " + op.En);
            }
            // VideoTXL's LocalPlayer only sends the state update: only that one is offered for it.
            var localType = TripwireModel.ResolveType("Texel.LocalPlayer");
            if (localType != null)
                CollectionAssert.AreEqual(new[] { "_TxlStateUpdate" }, PresetResolver.Notifications(localType).Select(n => n.Callback).ToArray());
            CollectionAssert.IsEmpty(missing);
        }

        static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var world = new GameObject("VRCWorld");
            world.AddComponent<VRC.SDK3.Components.VRCSceneDescriptor>().spawns = new[] { world.transform };

            foreach (var (id, prefabPath, _) in Assets)
            {
                var p = AssetPresets.Get(id);
                var asset = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
                asset.name = "Asset_" + id;
                var script = ScriptIn(asset, p);
                var t = new GameObject("Listener_" + id).AddComponent<TripwireTrigger>();
                var notes = PresetResolver.Notifications(script.GetType());
                for (int k = 0; k < notes.Count; k++)
                {
                    var hit = new GameObject("Hit_" + id + "_" + k);
                    var e = new KEvent { eventId = "ScriptNotified", listenTarget = script.gameObject };
                    Assert.IsTrue(PresetResolver.Apply(e, script.GetType(), notes[k]));
                    e.actions.Add(new KAction { actionId = "GameObject.ToggleActive", args = { new KArg { source = KArgSource.Objects, objects = { hit } } } });
                    t.events.Add(e);
                }
                // Every operation, so the generated calls compile against the real classes.
                var use = new KEvent { eventId = "Custom", name = "UseAll" };
                foreach (var op in PresetResolver.Operations(script.GetType()))
                    use.actions.AddRange(PresetResolver.Actions(script.GetType(), op, new List<Object> { script.gameObject }));
                t.events.Add(use);
            }

            Directory.CreateDirectory(SceneDir);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        static UdonBehaviour Backing(Component script) => script.GetComponents<UdonBehaviour>()
            .First(ub => UdonSharpEditor.UdonSharpEditorUtility.GetUdonSharpBehaviourType(ub) == script.GetType());

        [UnityTest]
        public IEnumerator RealAssetsTakeTheTriggerAsListener()
        {
            if (Installed().Count() < AssetPresets.All.Count && !EditorApplication.isPlaying)
                Assert.Ignore("Preset assets not installed (scripts/fetch-test-assets.sh).");
            LogAssert.ignoreFailingMessages = true;
            if (!EditorApplication.isPlaying && Root("Listener_protv") == null)
            {
                BuildScene();
                TripwireCompiler.ApplyAll(TripwireCompiler.SceneTriggers());
            }
            yield return new RecompileScripts(false);
            LogAssert.ignoreFailingMessages = true;
            if (!EditorApplication.isPlaying)
            {
                Assert.AreEqual(TripwireCompiler.State.UpToDate, TripwireCompiler.ApplyAll(TripwireCompiler.SceneTriggers()));
                Assert.IsFalse(UdonSharpProgramAsset.AnyUdonSharpScriptHasError(), "generated U# compiles against the real assets");
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            }

            NoDomainReloadOnPlay();
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;

            var listeners = Assets.ToDictionary(a => a.preset, a => Root("Listener_" + a.preset).GetComponent<UdonBehaviour>());
            for (int i = 0; i < 600 && !listeners.Values.All(ub => Flag(ub, "_isReady") && Flag(ub, "_hasDoneStart")); i++) yield return null;
            for (int i = 0; i < 30; i++) yield return null; // let the assets finish their own start-up

            var problems = new List<string>();
            var firedByAsset = new List<string>();
            foreach (var (id, _, field) in Assets)
            {
                var listener = listeners[id];
                var p = AssetPresets.Get(id);
                var asset = Backing(ScriptIn(Root("Asset_" + id), p));
                if (Flag(listener, "_hasError")) problems.Add(id + ": the trigger's program halted");
                if (!asset.TryGetProgramVariable(field, out object stored)) { problems.Add(id + ": no variable " + field); continue; }
                var flat = new List<object>();
                foreach (var x in stored as Array ?? new object[0])
                    if (x is Array inner) flat.AddRange(inner.Cast<object>()); else flat.Add(x);
                if (!flat.Contains(listener)) problems.Add(id + ": trigger not in " + field + " (" + flat.Count + " entries" + (Flag(asset, "_hasError") ? ", asset program halted" : "") + ")");

                // The asset calls listeners with SendCustomEvent(name): each preset name reaches its actions.
                var notes = PresetResolver.Notifications(ScriptIn(Root("Asset_" + id), p).GetType());
                for (int k = 0; k < notes.Count; k++)
                {
                    var hit = Root("Hit_" + id + "_" + k);
                    // Toggled already: the asset itself sent this notification during start-up.
                    if (!hit.activeSelf) firedByAsset.Add(id + " " + notes[k].Callback);
                    bool before = hit.activeSelf;
                    listener.SendCustomEvent(notes[k].Callback);
                    if (hit.activeSelf == before) problems.Add(id + ": " + notes[k].Callback + " did not run its actions");
                }
            }
            if (Root("Asset_videotxl") != null)
            {
                var txl = Backing(ScriptIn(Root("Asset_videotxl"), AssetPresets.Get("videotxl")));
                txl.TryGetProgramVariable("handlerEvents", out object events);
                var names = (events as Array)?.Cast<object>().OfType<Array>().SelectMany(a => a.Cast<object>()).OfType<string>().ToList() ?? new List<string>();
                if (!names.Contains("_TxlStateUpdate")) problems.Add("videotxl: callback name not registered (" + string.Join(", ", names) + ")");
            }
            Debug.Log("[TripwireTest] notifications the assets sent by themselves: " + string.Join(", ", firedByAsset));
            CollectionAssert.IsEmpty(problems);

            yield return new ExitPlayMode();
        }
    }
}
