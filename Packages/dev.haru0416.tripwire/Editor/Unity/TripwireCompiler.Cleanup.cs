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
    // Generated scripts nothing uses any more: found through open scenes, the prefab stage and saved files' dependencies.
    public static partial class TripwireCompiler
    {
        /// <summary>
        /// Delete generated scripts (and their program assets) that no open trigger or behaviour and no saved scene or
        /// prefab uses.
        /// Saved files are checked through asset dependencies, so a class still referenced on disk is kept until saved.
        /// </summary>
        internal static int DeleteUnusedScripts(IEnumerable<string> alsoKeep)
        {
            if (!Directory.Exists(OutputDir)) return 0;
            var keep = new HashSet<string>(alsoKeep, StringComparer.Ordinal);
            foreach (var t in SceneTriggers())
                if (!string.IsNullOrEmpty(t.generatedClass)) keep.Add(t.generatedClass);
            // Behaviours in open scenes, saved or not: a trigger may point elsewhere (values pasted from another
            // trigger) while its object still runs the old program, which deleting would leave empty.
            foreach (var ub in InOpenScenesAndPrefab<UdonBehaviour>())
                if (ub.programSource is UdonSharpProgramAsset program && program.sourceCsScript != null)
                    keep.Add(Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(program.sourceCsScript)));

            var containers = AssetDatabase.FindAssets("t:Scene t:Prefab", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            foreach (var dep in AssetDatabase.GetDependencies(containers, false))
                if (dep.StartsWith(OutputDir + "/", StringComparison.Ordinal))
                    keep.Add(Path.GetFileNameWithoutExtension(dep));

            int deleted = 0;
            foreach (var cs in Directory.GetFiles(OutputDir, CodeGenerator.ClassPrefix + "*.cs"))
            {
                var name = Path.GetFileNameWithoutExtension(cs);
                if (keep.Contains(name)) continue;
                var csPath = OutputDir + "/" + name + ".cs";
                AssetDatabase.DeleteAsset(Path.ChangeExtension(csPath, ".asset"));
                AssetDatabase.DeleteAsset(csPath);
                deleted++;
            }
            return deleted;
        }

        [MenuItem(TripwireMenus.DeleteUnused, true)] static bool NotPlayingDelete() => TripwireMenus.NotPlaying();
        [MenuItem(TripwireMenus.DeleteUnused, false, 1020)]
        internal static void DeleteUnusedMenu()
        {
            int n = DeleteUnusedScripts(new string[0]);
            AssetDatabase.Refresh();
            Debug.Log("[Tripwire] " + Texts.T("Deleted " + n + " unused generated script(s).", "使われていない生成スクリプトを " + n + " 個削除しました。"));
        }
    }
}
