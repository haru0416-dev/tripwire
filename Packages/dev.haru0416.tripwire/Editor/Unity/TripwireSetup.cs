using System.Collections.Generic;
using Tripwire.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tripwire.Editor
{
    /// <summary>What a scene needs besides the triggers: a VRCWorld, without which ClientSim doesn't start and nothing uploads.</summary>
    [InitializeOnLoad]
    internal static class TripwireSetup
    {
        const string WorldPrefab = "Packages/com.vrchat.worlds/Samples/UdonExampleScene/Prefabs/VRCWorld.prefab";

        static readonly Dictionary<Scene, bool> hasWorld = new Dictionary<Scene, bool>();

        static TripwireSetup() => EditorApplication.hierarchyChanged += hasWorld.Clear; // looked up again after any change

        public static string MissingWorldMessage => Texts.T(
            "This scene has no VRCWorld (VRChat's scene descriptor). Play mode testing (ClientSim) and uploading need one.",
            "このシーンには VRCWorld（VRChat のシーン設定）がありません。Play\u00A0での確認（ClientSim）と公開に必要です。");

        /// <summary>A scene (not a prefab being edited) without a VRC scene descriptor.</summary>
        public static bool MissingWorld(Scene scene)
        {
            if (!scene.IsValid() || EditorApplication.isPlaying || PrefabStageUtility.GetCurrentPrefabStage()?.scene == scene) return false;
            if (!hasWorld.TryGetValue(scene, out var found))
            {
                found = false;
                foreach (var root in scene.GetRootGameObjects())
                    if (root.GetComponentInChildren<VRC.SDKBase.VRC_SceneDescriptor>(true) != null) { found = true; break; }
                hasWorld[scene] = found;
            }
            return !found;
        }

        public static void AddWorld(Scene scene)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WorldPrefab);
            GameObject world;
            if (prefab != null) world = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            else
            {
                // The SDK moved its prefab: a plain descriptor at the origin does the same job.
                world = new GameObject("VRCWorld");
                SceneManager.MoveGameObjectToScene(world, scene);
                world.AddComponent<VRC.SDK3.Components.VRCSceneDescriptor>();
            }
            Undo.RegisterCreatedObjectUndo(world, Texts.T("Add VRCWorld", "VRCWorld を置く"));
            hasWorld.Clear();
        }
    }
}
