using System.IO;
using System.Linq;
using Tripwire.Core;
using Tripwire.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Tripwire.Tests
{
    // Dev tool: builds the package's sample scene (Samples~/Recipes) and checks it as someone who imports it would.
    //   Build: -executeMethod Tripwire.Tests.SampleBuilder.Build   → Packages/dev.haru0416.tripwire/Samples~/Recipes
    //   Check: -executeMethod Tripwire.Tests.SampleBuilder.Check   → copies the sample into Assets/Samples (as the
    //          Package Manager does), opens it and applies it; run it twice (the first run writes the scripts, the second
    //          finds them compiled). Results in Logs/sample-check.txt. Remove Assets/Samples afterwards.
    // The scene holds no generated scripts: Tripwire writes them in the importing project before Play.
    public static class SampleBuilder
    {
        const string BuildDir = "Assets/TripwireSampleBuild";
        const string SampleDir = "Packages/dev.haru0416.tripwire/Samples~/Recipes";
        const string ImportDir = "Assets/Samples/Tripwire/Recipes";
        const string SceneName = "Recipes.unity";

        public static void Build()
        {
            if (AssetDatabase.IsValidFolder(BuildDir)) AssetDatabase.DeleteAsset(BuildDir);
            AssetDatabase.CreateFolder("Assets", "TripwireSampleBuild");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            Instantiate("Packages/com.vrchat.worlds/Samples/UdonExampleScene/Prefabs/VRCWorld.prefab", new Vector3(0, 0, -4));
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(3, 1, 3);

            // Recipe: click to show / hide a mirror.
            var mirror = Instantiate("Packages/com.vrchat.worlds/Samples/UdonExampleScene/Prefabs/VRCMirror.prefab", new Vector3(-3, 1.5f, 3));
            mirror.name = "Mirror";
            var mirrorSwitch = Button("Mirror Switch", new Vector3(-3, 1, 0));
            var toggle = new KAction { actionId = "GameObject.ToggleActive", args = { new KArg { source = KArgSource.Objects, objects = { mirror } } } };
            mirrorSwitch.events.Add(new KEvent { eventId = EventCatalog.InteractId, interactText = "Mirror", actions = { toggle } });
            mirrorSwitch.comment = "クリックで鏡を表示・非表示 / Click to show or hide the mirror";

            // Recipe: a switch everyone shares (the starter card), driving a light.
            var lamp = new GameObject("Lamp", typeof(Light));
            lamp.transform.position = new Vector3(3, 2.5f, 1);
            var shared = Button("Shared Switch", new Vector3(3, 1, 0));
            TripwireTriggerEditor.Starters.First(s => s.En == "A switch everyone shares").Make(shared);
            shared.events.First(e => e.eventId == EventCatalog.VariableChangedId).actions[0].args[0] = new KArg { source = KArgSource.Objects, objects = { lamp } };
            shared.comment = "全員で共有するスイッチ / A switch everyone shares";

            // The SDK points the world prefab's behaviours at this project's serialized programs: overrides that would
            // dangle in anyone else's project. The prefab's own values are enough there.
            foreach (var root in scene.GetRootGameObjects())
            {
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(root)) continue;
                var mods = PrefabUtility.GetPropertyModifications(root);
                if (mods == null) continue;
                var kept = mods.Where(m => m.propertyPath != "serializedProgramAsset").ToArray();
                if (kept.Length != mods.Length) PrefabUtility.SetPropertyModifications(root, kept);
            }

            var path = BuildDir + "/" + SceneName;
            EditorSceneManager.SaveScene(scene, path);
            AssetDatabase.Refresh();

            // Samples~ is hidden from the AssetDatabase: copy the files (with their .meta, so references keep their GUIDs).
            if (Directory.Exists(SampleDir)) Directory.Delete(SampleDir, true);
            Directory.CreateDirectory(SampleDir);
            foreach (var f in new[] { SceneName, SceneName + ".meta" }) File.Copy(BuildDir + "/" + f, SampleDir + "/" + f);
            AssetDatabase.DeleteAsset(BuildDir);
            File.Delete(BuildDir + ".meta");
            Debug.Log("SAMPLE built: " + SampleDir);
        }

        public static void Check()
        {
            var log = new System.Text.StringBuilder();
            if (!Directory.Exists(ImportDir))
            {
                // As the Package Manager's "Import" does: copy the sample folder into Assets.
                Directory.CreateDirectory(ImportDir);
                foreach (var f in Directory.GetFiles(SampleDir)) File.Copy(f, ImportDir + "/" + Path.GetFileName(f));
                AssetDatabase.Refresh();
                log.AppendLine("imported into " + ImportDir);
            }
            EditorSceneManager.OpenScene(ImportDir + "/" + SceneName);

            int missingScripts = 0, missingPrefabs = 0;
            foreach (var go in Object.FindObjectsOfType<GameObject>(true))
            {
                missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                if (PrefabUtility.IsPrefabAssetMissing(go)) missingPrefabs++;
            }
            log.AppendLine($"missing scripts {missingScripts}, missing prefabs {missingPrefabs}");

            var triggers = TripwireCompiler.SceneTriggers();
            foreach (var t in triggers)
            {
                var g = TripwireCompiler.Generate(t);
                log.AppendLine($"trigger {t.name}: errors {g.Diagnostics.Count(d => d.Severity == Severity.Error)}, warnings {g.Diagnostics.Count(d => d.Severity == Severity.Warning)}"
                               + string.Concat(g.Diagnostics.Select(d => "\n  - " + d.Message)));
            }
            var state = TripwireCompiler.ApplyAll(triggers);
            log.AppendLine("apply: " + state + ", generated behaviours " + triggers.Count(t => t.generated != null) + "/" + triggers.Count);
            File.AppendAllText("Logs/sample-check.txt", log + "\n");
            Debug.Log("SAMPLE check:\n" + log);
        }

        static GameObject Instantiate(string prefabPath, Vector3 at)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = at;
            return go;
        }

        static TripwireTrigger Button(string name, Vector3 at)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = at;
            go.transform.localScale = new Vector3(0.6f, 0.6f, 0.2f);
            return go.AddComponent<TripwireTrigger>();
        }
    }
}
