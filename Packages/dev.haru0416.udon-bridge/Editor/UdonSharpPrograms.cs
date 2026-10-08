using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using VRC.Udon;

namespace UdonBridge
{
    public static class UdonSharpPrograms
    {
        /// <summary>The script's .asset, created if missing. <paramref name="plainFields"/>: skip U#'s upgrade pass.</summary>
        public static UdonSharpProgramAsset EnsureProgramAsset(string scriptPath, bool plainFields = false)
        {
            var assetPath = Path.ChangeExtension(scriptPath, ".asset");
            var existing = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(assetPath);
            if (existing != null) return existing;
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
            if (script == null) return null;
            var programAsset = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
            programAsset.sourceCsScript = script;
            if (plainFields) programAsset.ScriptVersion = UdonSharpProgramVersion.CurrentVersion;
            AssetDatabase.CreateAsset(programAsset, assetPath);
            AssetDatabase.SaveAssetIfDirty(programAsset);
            return programAsset;
        }

        /// <summary>U# refuses to serialize into a behaviour until its program asset is upgraded and compiled.</summary>
        public static bool IsReady(UdonSharpProgramAsset a) =>
            a.ScriptVersion >= UdonSharpProgramVersion.CurrentVersion && a.CompiledVersion >= UdonSharpProgramVersion.CurrentVersion;

        // UdonSharp internals used to move an UdonBehaviour to another U# program (the same steps U# runs for pasted
        // components): point a new proxy at the existing backing behaviour, then run U#'s setup on it.
        const BindingFlags Internal = BindingFlags.NonPublic | BindingFlags.Static;
        static readonly MethodInfo SetIgnoreEvents = typeof(UdonSharpEditorUtility).GetMethod("SetIgnoreEvents", Internal);
        static readonly MethodInfo SetBackingUdonBehaviour = typeof(UdonSharpEditorUtility).GetMethod("SetBackingUdonBehaviour", Internal);
        static readonly MethodInfo RunBehaviourSetupWithUndo = typeof(UdonSharpEditorUtility).GetMethod("RunBehaviourSetupWithUndo", Internal);
        static readonly FieldInfo SerializedProgramAssetField = typeof(UdonBehaviour).GetField("serializedProgramAsset", BindingFlags.NonPublic | BindingFlags.Instance);

        static readonly FieldInfo CompileWatchers = typeof(UdonSharpProgramAsset).Assembly
            .GetType("UdonSharpEditor.UdonSharpAssetCompileWatcher")?.GetField("_fileSystemWatchers", Internal);
        static readonly List<FileSystemWatcher> pausedWatchers = new List<FileSystemWatcher>();


        /// <summary>UdonSharp's compile-on-save, for a tool that compiles saves itself. False: this SDK has none.</summary>
        public static bool PauseCompileWatcher(bool pause)
        {
            if (!pause) { foreach (var w in pausedWatchers) w.EnableRaisingEvents = true; pausedWatchers.Clear(); return true; }
            if (CompileWatchers == null) return false;
            foreach (var w in CompileWatchers.GetValue(null) as FileSystemWatcher[] ?? Array.Empty<FileSystemWatcher>())
                if (w != null && w.EnableRaisingEvents) { w.EnableRaisingEvents = false; pausedWatchers.Add(w); }
            return true;
        }

        static readonly Type EditorCache = typeof(UdonSharpProgramAsset).Assembly.GetType("UdonSharp.UdonSharpEditorCache");
        static readonly PropertyInfo CacheInstance = EditorCache?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        static readonly PropertyInfo LastDiagnostics = EditorCache?.GetProperty("LastCompileDiagnostics", BindingFlags.NonPublic | BindingFlags.Instance);

        /// <summary>
        /// The errors of UdonSharp's last compile as (file, line, message), or null when this SDK doesn't expose them.
        /// U# compiles every script together: one script's error stops all of them.
        /// </summary>
        public static List<(string file, int line, string message)> LastCompileErrors()
        {
            if (CacheInstance == null || LastDiagnostics == null) return null;
            if (!(LastDiagnostics.GetValue(CacheInstance.GetValue(null)) is Array diagnostics)) return null;
            var errors = new List<(string, int, string)>();
            foreach (var d in diagnostics)
            {
                var t = d.GetType();
                if (t.GetField("severity")?.GetValue(d)?.ToString() != "Error") continue;
                errors.Add(((string)t.GetField("file")?.GetValue(d) ?? "", (int)(t.GetField("line")?.GetValue(d) ?? 0), (string)t.GetField("message")?.GetValue(d) ?? ""));
            }
            return errors;
        }

        public static bool SwapAvailable => SetIgnoreEvents != null && SetBackingUdonBehaviour != null && RunBehaviourSetupWithUndo != null && SerializedProgramAssetField != null;

        /// <summary>In place, so references to the behaviour survive. False: the caller recreates it.</summary>
        public static bool SwapProgram(UdonBehaviour ub, Type type, UdonSharpProgramAsset programAsset, string undoName)
        {
            if (!SwapAvailable) return false;
            var oldProxy = UdonSharpEditorUtility.GetProxyBehaviour(ub);
            // A behaviour switched off (to be switched on later by something else) stays off: the new proxy starts
            // enabled, and U#'s setup copies the proxy's state to the backing behaviour.
            bool wasEnabled = oldProxy != null ? oldProxy.enabled : ub.enabled;
            Undo.RecordObject(ub, undoName);
            ub.programSource = programAsset;
            SerializedProgramAssetField.SetValue(ub, programAsset.SerializedProgramAsset);

            UdonSharpBehaviour proxy;
            SetIgnoreEvents.Invoke(null, new object[] { true });
            try
            {
                if (oldProxy != null) Undo.DestroyObjectImmediate(oldProxy);
                proxy = (UdonSharpBehaviour)Undo.AddComponent(ub.gameObject, type);
                proxy.enabled = wasEnabled;
                SetBackingUdonBehaviour.Invoke(null, new object[] { proxy, ub });
            }
            finally
            {
                SetIgnoreEvents.Invoke(null, new object[] { false });
            }
            RunBehaviourSetupWithUndo.Invoke(null, new object[] { proxy });
            if (ub.enabled != wasEnabled) ub.enabled = wasEnabled;
            EditorUtility.SetDirty(ub);
            return UdonSharpEditorUtility.GetProxyBehaviour(ub) == proxy && ub.programSource == programAsset;
        }
    }
}
