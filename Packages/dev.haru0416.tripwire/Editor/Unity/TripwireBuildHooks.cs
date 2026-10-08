using System.Linq;
using Tripwire.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDKBase.Editor.BuildPipeline;

namespace Tripwire.Editor
{
    /// <summary>
    /// Before a world build: make sure every trigger is generated, attached and bound.
    /// Runs after UdonSharp's "still compiling" check (-1) and before its compile step (100).
    /// </summary>
    internal sealed class TripwireBuildRequested : IVRCSDKBuildRequestedCallback
    {
        public int callbackOrder => 50;

        public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType)
        {
            if (requestedBuildType != VRCSDKRequestedBuildType.Scene) return true;

            switch (TripwireCompiler.ApplyAll(TripwireCompiler.SceneTriggers()))
            {
                case TripwireCompiler.State.UpToDate:
                    return true;
                case TripwireCompiler.State.NeedsScripts:
                    EditorUtility.DisplayDialog("Tripwire Trigger",
                        Texts.T("New scripts were generated for your triggers. Wait for them to compile, then build again.",
                                "トリガーのスクリプトを新しく作りました。コンパイルが終わるのを待ってから、もう一度ビルドしてください。"), "OK");
                    return false;
                case TripwireCompiler.State.Blocked:
                    EditorUtility.DisplayDialog("Tripwire Trigger", TripwireCompiler.BlockedReason ?? "", "OK");
                    return false;
                case TripwireCompiler.State.ApplyFailed:
                    var reasons = TripwireCompiler.SceneTriggers().Select(TripwireCompiler.FailureOf).Where(m => m != null).Distinct().Take(3);
                    EditorUtility.DisplayDialog("Tripwire Trigger",
                        Texts.T("Some triggers couldn't be applied, so the build stopped:\n\n", "反映できないトリガーがあるので、ビルドを止めました。\n\n") + string.Join("\n\n", reasons), "OK");
                    return false;
                default:
                    EditorUtility.DisplayDialog("Tripwire Trigger",
                        Texts.T("Some triggers have errors (see the Console and the trigger Inspectors). Fix them and build again.",
                                "エラーのあるトリガーがあります（コンソールと各トリガーの Inspector を見てください）。直してから、もう一度ビルドしてください。"), "OK");
                    return false;
            }
        }
    }

    /// <summary>
    /// The authoring component never ships: remove it from scenes being built (VRChat's IEditorOnly stripping does the
    /// same on upload). In play mode it stays, so its Inspector can show live values and run events.
    /// </summary>
    internal sealed class TripwireStripper : IProcessSceneWithReport
    {
        public int callbackOrder => -100;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (!BuildPipeline.isBuildingPlayer) return;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<TripwireTrigger>(true))
                    Object.DestroyImmediate(t);
        }
    }
}
