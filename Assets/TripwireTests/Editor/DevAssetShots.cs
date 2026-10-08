using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Tripwire.Tests
{
    /// <summary>
    /// Dev only: screenshots of other VRChat assets' Inspectors (installed with scripts/fetch-test-assets.sh), to study
    /// their editor UI. Each step shows one component's Inspector in a window; scripts/asset-screenshots.sh captures it.
    /// Components are found by type name, so this compiles without the assets.
    /// </summary>
    public static class DevAssetShots
    {
        static readonly (string prefab, string type)[] Steps =
        {
            ("Packages/idv.jlchntoz.vvmw/VVMW (On-Screen Controls).prefab", "Core"),
            ("Packages/idv.jlchntoz.vvmw/VVMW (On-Screen Controls).prefab", "FrontendHandler"),
            ("Packages/idv.jlchntoz.vvmw/VVMW (On-Screen Controls).prefab", "UIHandler"),
            ("Packages/dev.architech.protv/Simple (ProTV).prefab", "TVManager"),
            ("Packages/idv.jlchntoz.lazyswitch/Lazy Switch Sample.prefab", "LazySwitch"),
            ("Packages/com.texelsaur.video/Runtime/Prefabs/Sync Video Player.prefab", "SyncPlayer"),
        };

        static int step, frames;
        static bool waiting;
        static EditorWindow inspector;

        public static void Run()
        {
            foreach (var f in Directory.GetFiles("Logs", "asset-*")) File.Delete(f);
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            // Unity's own Inspector, as people see it (U# editors draw through it).
            var inspectorType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow");
            inspector = (EditorWindow)ScriptableObject.CreateInstance(inspectorType);
            inspector.Show();
            inspector.position = new Rect(20, 40, 560, 1040);
            EditorApplication.update += Tick;
        }

        static Component Find(string prefab, string type)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
            if (asset == null) return null;
            var root = GameObject.Find(asset.name) ?? (GameObject)PrefabUtility.InstantiatePrefab(asset);
            return root.GetComponentsInChildren<Component>(true).FirstOrDefault(c => c != null && c.GetType().Name == type);
        }

        static void Tick()
        {
            frames++;
            if (inspector != null) inspector.Repaint();
            if (waiting)
            {
                if (File.Exists("Logs/asset-done-" + step)) { waiting = false; step++; frames = 0; }
                return;
            }
            if (frames == 1)
            {
                if (step == Steps.Length) { EditorApplication.update -= Tick; EditorApplication.Exit(0); return; }
                var c = Find(Steps[step].prefab, Steps[step].type);
                // Only this component expanded, so it is the one in view.
                if (c != null)
                {
                    foreach (var other in c.gameObject.GetComponents<Component>())
                        if (other != null) UnityEditorInternal.InternalEditorUtility.SetIsInspectorExpanded(other, other == c || other is VRC.Udon.UdonBehaviour);
                    Selection.activeGameObject = c.gameObject;
                }
                File.WriteAllText("Logs/asset-name-" + step, c == null ? "missing " + Steps[step].type : c.GetType().FullName);
            }
            if (frames >= 120)
            {
                File.WriteAllText("Logs/asset-ready-" + step, "");
                waiting = true;
            }
        }
    }

    public class AssetShotWindow : EditorWindow
    {
        UnityEditor.Editor editor;
        Vector2 scroll;

        public void Show(Component c)
        {
            if (editor != null) DestroyImmediate(editor);
            editor = c == null ? null : UnityEditor.Editor.CreateEditor(c);
            titleContent = new GUIContent(c == null ? "missing" : c.GetType().Name);
        }

        void OnGUI()
        {
            if (editor == null) return;
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.InspectorTitlebar(true, editor.target, false);
            editor.OnInspectorGUI();
            EditorGUILayout.EndScrollView();
        }
    }
}
