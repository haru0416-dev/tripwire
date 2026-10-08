using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Tripwire.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tripwire.Editor
{
    /// <summary>
    /// Export for distribution: the selected prefabs, scenes or folders and what they use, as a .unitypackage that works
    /// without Tripwire. Scenes and prefabs lose the Tripwire component (Unity can't save a prefab with a missing
    /// script); the generated scripts go in, and nothing from Packages/ (the buyer installs those packages).
    /// </summary>
    internal static class TripwireExport
    {
        internal sealed class Plan
        {
            /// <summary>The selected assets (a folder stands for what is in it).</summary>
            public List<string> Roots = new List<string>();
            /// <summary>Everything that goes in: the selection and what it uses, under Assets/.</summary>
            public List<string> Files = new List<string>();
            /// <summary>Scenes and prefabs that lose Tripwire components (theirs, or what they change on a prefab's).</summary>
            public List<string> Stripped = new List<string>();
            /// <summary>Packages the selection uses, which the person receiving it must have.</summary>
            public List<string> Packages = new List<string>();
            /// <summary>What stops the export (not applied, unsaved, binary).</summary>
            public List<string> Problems = new List<string>();
            public List<string> Notes = new List<string>();
            /// <summary>
            /// Files going in with unsaved changes in the editor (the VRChat SDK marks a scene changed when it opens):
            /// the saved file is what is exported, so they are saved first, when the person says so.
            /// </summary>
            public List<string> Unsaved = new List<string>();
            /// <summary>
            /// Keep the cards: the package then needs Tripwire, and the person receiving it can edit them (an editable
            /// version, next to the one that works without Tripwire).
            /// </summary>
            public bool KeepTripwire;
            public bool CanWrite => Files.Count > 0 && Problems.Count == 0;
        }

        static string T(string en, string ja) => Texts.T(en, ja);

        static string scriptGuid;

        /// <summary>The guid of TripwireTrigger's script, which marks its components in YAML.</summary>
        internal static string ScriptGuid => scriptGuid ?? (scriptGuid = AssetDatabase.FindAssets("TripwireTrigger t:MonoScript")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => AssetDatabase.LoadAssetAtPath<MonoScript>(p)?.GetClass() == typeof(TripwireTrigger))
            .Select(AssetDatabase.AssetPathToGUID).FirstOrDefault());

        static bool IsYamlAsset(string path) => path.EndsWith(".prefab") || path.EndsWith(".unity");

        // TextMesh Pro's essential resources live in each project's Assets (Unity offers to import them when TextMesh Pro
        // is first used): needed, like a package, not shipped.
        const string TmpEssentials = "Assets/TextMesh Pro/";

        internal static Plan MakePlan(IEnumerable<string> selected, bool keepTripwire = false)
        {
            var plan = new Plan { KeepTripwire = keepTripwire };
            foreach (var path in selected.Distinct())
            {
                if (AssetDatabase.IsValidFolder(path))
                    plan.Roots.AddRange(AssetDatabase.FindAssets("", new[] { path }).Select(AssetDatabase.GUIDToAssetPath).Where(p => !AssetDatabase.IsValidFolder(p)));
                else if (path.StartsWith("Assets/")) plan.Roots.Add(path);
            }
            plan.Roots = plan.Roots.Distinct().ToList();
            if (plan.Roots.Count == 0)
            {
                plan.Problems.Add(T("Select the prefabs, scenes or folders to export in the Project window.", "書き出すプレハブ・シーン・フォルダを、Project で選んでください。"));
                return plan;
            }

            foreach (var path in AssetDatabase.GetDependencies(plan.Roots.ToArray(), true).Union(plan.Roots).Distinct().OrderBy(p => p))
            {
                if (path.StartsWith(TmpEssentials)) { plan.Packages.Add("TextMesh Pro Essential Resources"); continue; }
                if (path.StartsWith("Assets/")) { if (!AssetDatabase.IsValidFolder(path)) plan.Files.Add(path); continue; }
                var info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(path);
                // Tripwire itself isn't needed when its component comes out.
                if (info != null && (keepTripwire || info.name != "dev.haru0416.tripwire")) plan.Packages.Add(info.displayName + " (" + info.name + ")");
            }
            plan.Packages = plan.Packages.Distinct().OrderBy(p => p).ToList();

            var contents = Stripped(plan.Files, out var binary);
            if (!keepTripwire) plan.Stripped = contents.Keys.OrderBy(p => p).ToList();
            foreach (var path in keepTripwire ? new List<string>() : binary)
                plan.Problems.Add(T(path + " is saved as binary, so Tripwire can't take its components out. Set Project Settings > Editor > Asset Serialization to Force Text.",
                                    path + " がバイナリで保存されているため、Tripwire のコンポーネントを外せません。Project Settings > Editor の Asset Serialization を Force Text にしてください。"));
            foreach (var path in plan.Files.Where(IsYamlAsset)) CheckApplied(path, plan);
            foreach (var path in plan.Files.Where(p => !plan.Unsaved.Contains(p) && IsUnsaved(p)))
            {
                plan.Unsaved.Add(path);
                plan.Problems.Add(T(path + " has unsaved changes, and what is exported is the saved file. Save it first.", path + " に保存していない変更があります。書き出すのは保存した内容なので、先に保存してください。"));
            }
            if (keepTripwire)
                plan.Notes.Add(T("The cards stay: the person receiving it needs Tripwire (from VCC), and can edit them.",
                                 "カードを残します。受け取る人は Tripwire を（VCC から）入れておく必要があり、カードとして直せます。"));
            else if (plan.Stripped.Count > 0)
                plan.Notes.Add(T("The person receiving it gets the gimmicks working as they are, without cards to edit (that would need Tripwire and your original).",
                                 "受け取った人の手元では、ギミックはそのまま動きますが、カードとしては直せません（直すには Tripwire と元のデータが要ります）。"));
            return plan;
        }

        /// <summary>The scenes and prefabs that change, with their new text; binary ones (that can't be read) apart.</summary>
        static Dictionary<string, string> Stripped(IEnumerable<string> files, out List<string> binary)
        {
            binary = new List<string>();
            var texts = new Dictionary<string, string>();
            foreach (var path in files.Where(IsYamlAsset))
            {
                var text = File.ReadAllText(path);
                if (Distribution.IsText(text)) texts[path] = text;
                else binary.Add(path);
            }
            // A scene or prefab may change a prefab's trigger (an override): those changes go with the trigger.
            var sources = texts.Select(kv => (guid: AssetDatabase.AssetPathToGUID(kv.Key), ids: Distribution.ComponentIds(kv.Value, ScriptGuid)))
                .Where(x => x.ids.Count > 0).ToDictionary(x => x.guid, x => x.ids);
            var changed = new Dictionary<string, string>();
            foreach (var kv in texts)
            {
                var stripped = Distribution.Strip(kv.Value, ScriptGuid, sources, out _);
                if (stripped != kv.Value) changed[kv.Key] = stripped;
            }
            return changed;
        }

        /// <summary>Every trigger of a prefab or an open scene is applied (else its generated script is missing or old).</summary>
        static void CheckApplied(string path, Plan plan)
        {
            if (path.EndsWith(".prefab"))
            {
                // Opened as in Prefab Mode: in the asset itself, triggers inside it that set each other's variables
                // aren't looked up (an asset never runs), so it would read as not applied.
                var contents = PrefabUtility.LoadPrefabContents(path);
                try { CheckApplied(path, contents.GetComponentsInChildren<TripwireTrigger>(true), plan); }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
                return;
            }
            var scene = SceneManager.GetSceneByPath(path);
            if (!scene.isLoaded)
            {
                if (File.ReadAllText(path).Contains(ScriptGuid))
                    plan.Notes.Add(T(path + " isn't open, so whether its triggers are applied wasn't checked. Open it once to check.",
                                     path + " は開いていないので、トリガーが反映済みかを確かめていません。一度開くと確かめられます。"));
                return;
            }
            if (scene.isDirty)
            {
                // What is exported is the saved file: an unsaved apply would be missing from it.
                plan.Unsaved.Add(path);
                plan.Problems.Add(T(path + " has unsaved changes, and what is exported is the saved scene. Save it first.", path + " に保存していない変更があります。書き出すのは保存した内容なので、先に保存してください。"));
            }
            CheckApplied(path, scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<TripwireTrigger>(true)), plan);
        }

        static void CheckApplied(string path, IEnumerable<TripwireTrigger> triggers, Plan plan)
        {
            using (TripwireModel.Batch())
                foreach (var t in triggers)
                {
                    var state = TripwireCompiler.GetState(t, TripwireCompiler.Generate(t));
                    if (state == TripwireCompiler.State.UpToDate) continue;
                    plan.Problems.Add(state == TripwireCompiler.State.HasErrors
                        ? T(path + ": the trigger on " + t.name + " has errors. Fix them first.", path + ": " + t.name + " のトリガーにエラーがあります。先に直してください。")
                        : T(path + ": the trigger on " + t.name + " isn't applied. Open it and press Apply now.", path + ": " + t.name + " のトリガーが反映されていません。開いて「今すぐ反映」を押してください。"));
                }
        }

        /// <summary>An asset (not a scene) edited in the editor and not saved: a loaded, changed asset, or the prefab open in Prefab Mode.</summary>
        static bool IsUnsaved(string path)
        {
            if (path.EndsWith(".unity")) return false; // scenes: CheckApplied
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == path && stage.scene.isDirty) return true;
            return AssetDatabase.IsMainAssetAtPathLoaded(path) && EditorUtility.IsDirty(AssetDatabase.LoadMainAssetAtPath(path));
        }

        /// <summary>Saves what the plan found unsaved ("Save them and check again").</summary>
        internal static void Save(IEnumerable<string> paths)
        {
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            foreach (var path in paths)
            {
                if (path.EndsWith(".unity")) UnityEditor.SceneManagement.EditorSceneManager.SaveScene(SceneManager.GetSceneByPath(path));
                else if (stage != null && stage.assetPath == path)
                {
                    PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, path);
                    stage.ClearDirtiness();
                }
                else AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadMainAssetAtPath(path));
            }
        }

        /// <summary>Writes the package. The project's own files are only read.</summary>
        internal static void Write(Plan plan, string output)
        {
            var changed = plan.KeepTripwire ? new Dictionary<string, string>() : Stripped(plan.Files, out _);
            var entries = new List<Distribution.Entry>();
            // The folders too, so the receiving project gets the same folder guids (no duplicates when updating).
            var folders = new SortedSet<string>();
            foreach (var path in plan.Files)
                for (var dir = Path.GetDirectoryName(path)?.Replace('\\', '/'); dir != null && dir.StartsWith("Assets/"); dir = Path.GetDirectoryName(dir)?.Replace('\\', '/'))
                    folders.Add(dir);
            foreach (var dir in folders)
                entries.Add(new Distribution.Entry { Guid = AssetDatabase.AssetPathToGUID(dir), PathName = dir, Meta = File.ReadAllBytes(dir + ".meta") });
            foreach (var path in plan.Files)
                entries.Add(new Distribution.Entry
                {
                    Guid = AssetDatabase.AssetPathToGUID(path),
                    PathName = path,
                    Meta = File.ReadAllBytes(path + ".meta"),
                    Content = changed.TryGetValue(path, out var text) ? new UTF8Encoding(false).GetBytes(text) : null,
                    File = changed.ContainsKey(path) ? null : path,
                });
            using (var file = File.Create(output)) Distribution.WritePackage(file, entries);
        }
    }
}
