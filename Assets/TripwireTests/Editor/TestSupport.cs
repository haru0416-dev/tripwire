using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.Udon;

namespace Tripwire.Tests
{
    /// <summary>Helpers shared by the Unity tests.</summary>
    public static class TestSupport
    {
        /// <summary>A root object of the active scene by name, or null.</summary>
        public static GameObject Root(string name) => SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == name);

        /// <summary>A private state flag of an UdonBehaviour (_isReady, _hasDoneStart, _hasError).</summary>
        public static bool Flag(UdonBehaviour ub, string field) =>
            (bool)typeof(UdonBehaviour).GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(ub);
    }

    /// <summary>
    /// Base for tests that enter play mode: they switch domain reload off (ClientSim expects it), and this puts the
    /// project default back afterwards, even when the test fails.
    /// </summary>
    public abstract class PlayModeTest
    {
        /// <summary>Call right before EnterPlayMode: ClientSim needs play mode without a domain reload.</summary>
        protected static void NoDomainReloadOnPlay()
        {
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        }

        [TearDown]
        public void RestorePlayModeOptions()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload; // Unity default
        }
    }
}
