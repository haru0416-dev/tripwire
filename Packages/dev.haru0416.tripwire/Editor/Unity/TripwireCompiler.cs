using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Tripwire.Core;
using UdonSharp;
using UdonBridge;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.Udon;
using Object = UnityEngine.Object;

namespace Tripwire.Editor
{
    /// <summary>
    /// Generates UdonSharp scripts for triggers and attaches them.
    ///
    /// A new program shape needs a domain reload before its class can be added as a component, so applying is two-phase:
    /// write scripts → (reload) → attach the behaviours and bind the dragged-in references.
    /// Programs are content-addressed (class name = hash of the source), so identical triggers share one script.
    /// </summary>
    [InitializeOnLoad]
    public static partial class TripwireCompiler
    {
        public const string OutputDir = "Assets/TripwireGenerated";
        const string PendingAttachKey = "Tripwire.PendingAttach";
        const string RetryKey = "Tripwire.AttachRetries";
        const string PlayAfterReloadKey = "Tripwire.PlayAfterReloadAt";
        /// <summary>A play request older than this is dropped (e.g. the generated scripts failed to compile).</summary>
        const float PlayRequestLifetime = 300f;
        static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        /// <summary>
        /// HasErrors: the trigger itself has errors. ApplyFailed: it is fine, but attaching or binding it failed (see
        /// <see cref="FailureOf"/>). Blocked: the project doesn't compile, so nothing can be applied (<see cref="BlockedReason"/>).
        /// </summary>
        public enum State { UpToDate, NeedsApply, NeedsScripts, HasErrors, ApplyFailed, Blocked }

        static readonly Dictionary<int, string> failures = new Dictionary<int, string>();

        /// <summary>Why the last apply of this trigger didn't finish, or null.</summary>
        public static string FailureOf(TripwireTrigger t) => t != null && failures.TryGetValue(t.GetInstanceID(), out var m) ? m : null;

        static bool Fail(TripwireTrigger t, string message)
        {
            failures[t.GetInstanceID()] = message;
            Debug.LogError("[Tripwire] " + message, t);
            return false;
        }

        static string blockedReason;

        /// <summary>Why nothing can be applied now (the project doesn't compile), or null.</summary>
        public static string BlockedReason => blockedReason
            ?? (EditorUtility.scriptCompilationFailed
                ? Texts.T("The project's scripts have errors, so Unity can't compile Tripwire's either. Fix the red errors in the Console; applying then continues by itself.",
                          "プロジェクトのスクリプトにエラーがあり、Unity が Tripwire のスクリプトもコンパイルできません。Console の赤いエラーを直すと、自動で反映が続きます。")
                : null);

        /// <summary>Scripts were written and the apply resumes after Unity compiles them.</summary>
        public static bool ResumePending => SessionState.GetBool(PendingAttachKey, false);

        /// <summary>Play was pressed while scripts compiled: it starts after the reload.</summary>
        public static bool PlayPending => SessionState.GetFloat(PlayAfterReloadKey, -1f) >= 0f;

        static TripwireCompiler()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.delayCall += ResumeAfterReload;
        }

        static void ResumeAfterReload()
        {
            if (!SessionState.GetBool(PendingAttachKey, false)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += ResumeAfterReload;
                return;
            }
            SessionState.EraseBool(PendingAttachKey);
            var result = ApplyAll(SceneTriggers());
            float requestedAt = SessionState.GetFloat(PlayAfterReloadKey, -1f);
            if (requestedAt >= 0f && result != State.NeedsScripts)
            {
                SessionState.EraseFloat(PlayAfterReloadKey);
                if (result == State.UpToDate && EditorApplication.timeSinceStartup - requestedAt < PlayRequestLifetime)
                    EditorApplication.EnterPlaymode();
                else if (result != State.UpToDate)
                    Notify(result == State.Blocked ? BlockedReason : Texts.T("Play didn't start: some triggers couldn't be applied. Their Inspectors say why.", "反映できないトリガーがあるので、Play を始めませんでした。理由はそれぞれの Inspector に出ています。"));
            }
        }

        /// <summary>Triggers in the open scenes and, when editing a prefab, in the prefab stage.</summary>
        public static List<TripwireTrigger> SceneTriggers()
        {
            return InOpenScenesAndPrefab<TripwireTrigger>();
        }

        /// <summary>Components in the open scenes and the prefab being edited (FindObjectsOfType misses the latter).</summary>
        internal static List<T> InOpenScenesAndPrefab<T>() where T : Component
        {
            var list = Object.FindObjectsOfType<T>(true).Where(c => !EditorUtility.IsPersistent(c)).ToList();
            var root = EditedPrefabRoot();
            if (root != null) list.AddRange(root.GetComponentsInChildren<T>(true));
            return list.Distinct().ToList();
        }

        internal static GameObject EditedPrefabRoot()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            return stage != null && stage.prefabContentsRoot != null ? stage.prefabContentsRoot : null;
        }

        public static GeneratedProgram Generate(TripwireTrigger t)
        {
            UdonApi.InstallEditorKnowledge();
            var g = CodeGenerator.Generate(TripwireModel.ToProgram(t));
            TripwireLoops.AddSceneWarnings(t, g);
            return g;
        }

        public static Type FindGeneratedType(string className)
        {
            var fullName = CodeGenerator.Namespace + "." + className;
            return TypeCache.GetTypesDerivedFrom<UdonSharpBehaviour>().FirstOrDefault(x => x.FullName == fullName);
        }

        public static State GetState(TripwireTrigger t, GeneratedProgram g)
        {
            if (g.HasErrors) return State.HasErrors;
            if (FindGeneratedType(g.ClassName) == null) return EditorUtility.scriptCompilationFailed ? State.Blocked : State.NeedsScripts;
            if (blockedReason != null) return State.Blocked;
            if (FailureOf(t) != null) return State.ApplyFailed;
            if (t.generated == null || t.generated.gameObject != t.gameObject || t.generatedClass != g.ClassName) return State.NeedsApply;
            return State.UpToDate;
        }

        /// <summary>
        /// Generate, write and attach. Returns UpToDate when every trigger is attached and bound,
        /// NeedsScripts when new scripts were written and a reload must happen first (attach resumes after it),
        /// HasErrors when some trigger has errors (others are still processed).
        /// </summary>
        public static State ApplyAll(IList<TripwireTrigger> triggers)
        {
            using (TripwireModel.Batch()) return ApplyAllNow(triggers);
        }

        static State ApplyAllNow(IList<TripwireTrigger> triggers)
        {
            var programs = new Dictionary<TripwireTrigger, GeneratedProgram>();
            bool errors = false;
            blockedReason = null;
            foreach (var t in triggers)
            {
                if (t != null) failures.Remove(t.GetInstanceID());
                TripwireMigration.Upgrade(t);
                if (TripwireMigration.IsNewer(t)) { Fail(t, TripwireMigration.NewerMessage); errors = true; continue; }
                var g = Generate(t);
                if (g.HasErrors)
                {
                    errors = true;
                    foreach (var d in g.Diagnostics.Where(d => d.Severity == Severity.Error))
                        Debug.LogError("[Tripwire] " + t.name + ": " + d, t);
                    continue;
                }
                programs[t] = g;
            }

            if (WriteScripts(programs.Values))
            {
                SessionState.SetBool(PendingAttachKey, true);
                // The generated classes load only when the whole project compiles.
                return EditorUtility.scriptCompilationFailed ? State.Blocked : State.NeedsScripts;
            }

            var assets = new List<UdonSharpProgramAsset>();
            foreach (var kv in programs.ToList())
            {
                var asset = EnsureProgramAsset(OutputDir + "/" + kv.Value.ClassName + ".cs");
                if (asset != null) { assets.Add(asset); continue; }
                Fail(kv.Key, Texts.T("Couldn't create the UdonSharp program asset for " + kv.Value.ClassName + ".", kv.Value.ClassName + " の UdonSharp プログラムアセットを作れませんでした。"));
                programs.Remove(kv.Key);
                errors = true;
            }
            if (!assets.All(IsReady))
            {
                // Now, not at U#'s next reload, so this apply can attach the behaviours.
                UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();
                if (UdonSharpCompileBlocked()) return State.Blocked;
                if (!assets.All(IsReady))
                {
                    // U# is mid-compile or mid-upgrade; finish attaching once it settles (bounded retries).
                    int attempts = SessionState.GetInt(RetryKey, 0);
                    if (attempts >= 20)
                    {
                        SessionState.EraseInt(RetryKey);
                        blockedReason = Texts.T("UdonSharp didn't finish compiling Tripwire's scripts. Press Apply now to try again.", "UdonSharp が Tripwire のスクリプトのコンパイルを終えませんでした。「今すぐ反映」でやり直してください。");
                        Debug.LogError("[Tripwire] " + blockedReason);
                        return State.Blocked;
                    }
                    SessionState.SetInt(RetryKey, attempts + 1);
                    SessionState.SetBool(PendingAttachKey, true);
                    EditorApplication.delayCall += ResumeAfterReload;
                    return State.NeedsScripts;
                }
                SessionState.EraseInt(RetryKey);
            }

            // Two passes so that triggers referencing other triggers bind to their (new) generated behaviours.
            var attached = new List<KeyValuePair<TripwireTrigger, GeneratedProgram>>();
            foreach (var kv in programs)
            {
                if (EnsureBehaviour(kv.Key, kv.Value)) attached.Add(kv);
                else errors = true;
            }
            foreach (var kv in attached)
                if (!Bind(kv.Key, kv.Value)) errors = true;

            if (!errors) return State.UpToDate;
            return triggers.Any(t => t != null && FailureOf(t) == null && !programs.ContainsKey(t)) ? State.HasErrors : State.ApplyFailed;
        }

        /// <summary>
        /// After a U# compile: whether it stopped at an error. U# compiles every script together, so an error in any
        /// script (usually the user's own) keeps Tripwire's from compiling; the reason names that script.
        /// </summary>
        static bool UdonSharpCompileBlocked()
        {
            var errors = UdonSharpPrograms.LastCompileErrors();
            if (errors == null ? !UdonSharpProgramAsset.AnyUdonSharpScriptHasError() : errors.Count == 0) return false;
            var other = errors?.FirstOrDefault(e => !e.file.Replace('\\', '/').Contains(OutputDir + "/"));
            blockedReason = other != null && other.Value.file != null
                ? Texts.T("UdonSharp stopped at an error in " + System.IO.Path.GetFileName(other.Value.file) + " (line " + (other.Value.line + 1) + "), one of the project's own scripts. Fix it (see the Console); then apply again.",
                          "UdonSharp のコンパイルが " + System.IO.Path.GetFileName(other.Value.file) + "（" + (other.Value.line + 1) + " 行目）のエラーで止まっています。Tripwire ではなくプロジェクトのスクリプトです。Console を見て直してから、もう一度反映してください。")
                : Texts.T("Tripwire's generated UdonSharp didn't compile. This is a Tripwire bug: please report it with the errors in the Console.",
                          "Tripwire が作った UdonSharp がコンパイルできませんでした。Tripwire の不具合なので、Console のエラーを添えて報告してください。");
            Debug.LogError("[Tripwire] " + blockedReason);
            return true;
        }

        /// <summary>
        /// Write missing/changed scripts. True if a domain reload is needed.
        /// Program assets are created only after the class is loaded: a program asset whose script Unity has not
        /// compiled yet makes any U# compile in between fail with "does not belong to a U# assembly".
        /// </summary>
        static bool WriteScripts(IEnumerable<GeneratedProgram> programs)
        {
            var written = new List<string>();
            bool missingType = false;
            foreach (var g in programs.GroupBy(x => x.ClassName).Select(x => x.First()))
            {
                var path = OutputDir + "/" + g.ClassName + ".cs";
                if (!File.Exists(path) || File.ReadAllText(path) != g.Source)
                {
                    Directory.CreateDirectory(OutputDir);
                    File.WriteAllText(path, g.Source, Utf8NoBom);
                    written.Add(path);
                }
                if (FindGeneratedType(g.ClassName) == null) missingType = true;
            }

            if (written.Count > 0)
            {
                AssetDatabase.StartAssetEditing();
                try
                {
                    foreach (var path in written)
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    // A reload happens anyway: drop scripts nothing uses in the same refresh (a stale broken script
                    // would otherwise keep the whole project from compiling after the trigger was fixed).
                    DeleteUnusedScripts(programs.Select(x => x.ClassName));
                }
                finally { AssetDatabase.StopAssetEditing(); }
            }

            if (written.Count > 0 || missingType)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return true;
            }
            return false;
        }

        // Generated scripts only declare plain public fields: nothing for U#'s serialization upgrade pass to rewrite.
        static UdonSharpProgramAsset EnsureProgramAsset(string csPath) => UdonSharpPrograms.EnsureProgramAsset(csPath, plainFields: true);

        static bool IsReady(UdonSharpProgramAsset a) => UdonSharpPrograms.IsReady(a);

        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingEditMode) return;
            var state = ApplyAll(SceneTriggers());
            if (state == State.UpToDate) return;

            EditorApplication.isPlaying = false;
            string message;
            if (state == State.NeedsScripts)
            {
                SessionState.SetFloat(PlayAfterReloadKey, (float)EditorApplication.timeSinceStartup);
                message = Texts.T("Tripwire made new scripts. Play starts by itself when Unity has compiled them.", "Tripwire がスクリプトを作りました。Unity のコンパイルが終わると、自動で Play が始まります。");
                Debug.Log("[Tripwire] " + message);
            }
            else
            {
                message = state == State.Blocked ? BlockedReason
                    : Texts.T("Play was stopped: some triggers couldn't be applied. Their Inspectors say why.", "反映できないトリガーがあるので Play を止めました。理由はそれぞれの Inspector に出ています。");
                Debug.LogError("[Tripwire] " + message);
            }
            Notify(message);
        }

        /// <summary>A message where the person is looking when Play doesn't start (the Console may be hidden).</summary>
        static void Notify(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            var content = new GUIContent(message);
            foreach (SceneView view in SceneView.sceneViews) view.ShowNotification(content, 6);
            EditorWindow.focusedWindow?.ShowNotification(content, 6);
        }

        [MenuItem(TripwireMenus.ApplyAll, true)] static bool NotPlayingApply() => TripwireMenus.NotPlaying();
        [MenuItem(TripwireMenus.ApplyAll, false, 1000)]
        public static void ApplyAllMenu()
        {
            var state = ApplyAll(SceneTriggers());
            Debug.Log("[Tripwire] " + (state == State.UpToDate ? Texts.T("Every trigger is applied.", "すべてのトリガーを反映しました。")
                : state == State.NeedsScripts ? Texts.T("Made new scripts; applying continues after Unity compiles them.", "スクリプトを作りました。Unity のコンパイルが終わると、自動で反映が続きます。")
                : state == State.Blocked ? BlockedReason
                : Texts.T("Some triggers couldn't be applied. Their Inspectors say why.", "反映できないトリガーがあります。理由はそれぞれの Inspector に出ています。")));
        }
    }
}
