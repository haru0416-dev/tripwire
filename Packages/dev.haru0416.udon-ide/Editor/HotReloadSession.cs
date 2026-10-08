using System;
using UnityEditor;
using UdonBridge;

namespace UdonIde
{
    /// <summary>In play mode, hot reloads .cs files saved under Assets; Unity's imports wait until play ends.</summary>
    [InitializeOnLoad]
    static class HotReloadSession
    {
        const string Pref = "UdonIde.HotReload";
        const string Menu = "Tools/Udon IDE/Hot Reload In Play Mode";
        static System.IO.FileSystemWatcher watcher;
        static readonly object gate = new object();
        static double changedAt = -1;
        static bool holding;

        static HotReloadSession()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.update += Tick;
            if (EditorApplication.isPlaying) Begin();
        }

        static bool On => EditorPrefs.GetBool(Pref, true);

        [MenuItem(Menu)]
        static void Toggle() { EditorPrefs.SetBool(Pref, !On); if (EditorApplication.isPlaying) { if (On) Begin(); else End(); } }
        [MenuItem(Menu, true)]
        static bool ToggleCheck() { UnityEditor.Menu.SetChecked(Menu, On); return true; }

        static void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode) Begin();
            if (state == PlayModeStateChange.ExitingPlayMode) End();
            if (state == PlayModeStateChange.EnteredEditMode && pendingRefresh) { pendingRefresh = false; AssetDatabase.Refresh(); }
        }

        static bool pendingRefresh;

        static void PauseUdonSharpWatcher(bool pause)
        {
            if (!UdonSharpPrograms.PauseCompileWatcher(pause) && pause)
                UnityEngine.Debug.LogWarning("[Udon IDE] UdonSharp's own compile-on-save couldn't be paused: each save compiles twice during play");
        }

        static void Begin()
        {
            if (!On || watcher != null || holding) return;
            if (!HotReload.Available) { UnityEngine.Debug.LogWarning("[Udon IDE] Hot reload doesn't work with this VRChat SDK: saves are imported as usual"); return; }
            // The watcher first: if it can't be made (the system's watch limit), nothing is held.
            try
            {
                watcher = new System.IO.FileSystemWatcher(System.IO.Path.GetFullPath("Assets"), "*.cs")
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = System.IO.NotifyFilters.LastWrite | System.IO.NotifyFilters.FileName,
                };
                System.IO.FileSystemEventHandler mark = (_, __) => { lock (gate) changedAt = Now; };
                watcher.Changed += mark;
                watcher.Created += mark;
                watcher.Renamed += (_, __) => { lock (gate) changedAt = Now; }; // editors that save to a temp file and rename it
                watcher.EnableRaisingEvents = true;
            }
            catch (Exception e)
            {
                watcher?.Dispose();
                watcher = null;
                UnityEngine.Debug.LogWarning("[Udon IDE] Hot reload is off for this play: " + e.Message);
                return;
            }
            HotReload.Remember();
            PauseUdonSharpWatcher(true);
            // Importing a script recreates the components' C# objects, dropping Udon's state; from the start of play,
            // since switching back from another editor refreshes at once.
            holding = true;
            AssetDatabase.DisallowAutoRefresh();
            EditorApplication.LockReloadAssemblies();
            AssetDatabase.StartAssetEditing();
        }

        static void End()
        {
            if (watcher != null) { watcher.EnableRaisingEvents = false; watcher.Dispose(); watcher = null; PauseUdonSharpWatcher(false); }
            if (holding)
            {
                holding = false;
                AssetDatabase.StopAssetEditing();
                AssetDatabase.AllowAutoRefresh();
                EditorApplication.UnlockReloadAssemblies();
                pendingRefresh = true; // the saved files still need Unity's import and C# compile
            }
        }

        static double Now => System.DateTime.UtcNow.Subtract(System.DateTime.MinValue).TotalSeconds;

        static void Tick()
        {
            if (watcher == null) return;
            lock (gate)
            {
                // Wait for the save to settle (editors write in several steps).
                if (changedAt < 0 || Now - changedAt < 0.15) return;
                changedAt = -1;
            }
            var r = HotReload.Apply();
            if (!r.Compiled) UnityEngine.Debug.LogWarning("[Udon IDE] Hot reload: U# has errors, the running code stays as it was.");
            else UnityEngine.Debug.Log($"[Udon IDE] Hot reload: {r.Swapped} behaviour(s) updated in {r.CompileMs + r.SwapMs:F0} ms" + (r.Notes.Count > 0 ? " (" + string.Join("; ", r.Notes) + ")" : ""));
        }
    }
}
