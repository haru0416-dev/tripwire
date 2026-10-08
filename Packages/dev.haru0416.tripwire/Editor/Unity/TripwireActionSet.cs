using System.Collections.Generic;
using System.Linq;
using Tripwire.Core;
using UnityEditor;
using UnityEngine;

namespace Tripwire.Editor
{
    /// <summary>
    /// Actions saved to reuse in other triggers (and to hand to other people as an asset): picked from "保存したアクション".
    /// References to scene objects are left out (an asset can't hold them); asset references (sounds, materials) stay.
    /// </summary>
    public sealed class TripwireActionSet : ScriptableObject
    {
        [TextArea] public string description = "";
        [SerializeReference] public List<KAction> actions = new List<KAction>();

        public const string IdPrefix = "saved:";

        internal static IEnumerable<(string guid, TripwireActionSet set)> All() =>
            AssetDatabase.FindAssets("t:" + nameof(TripwireActionSet))
                .Select(g => (g, AssetDatabase.LoadAssetAtPath<TripwireActionSet>(AssetDatabase.GUIDToAssetPath(g))))
                .Where(x => x.Item2 != null);

        internal static TripwireActionSet Of(string id) =>
            id != null && id.StartsWith(IdPrefix) ? AssetDatabase.LoadAssetAtPath<TripwireActionSet>(AssetDatabase.GUIDToAssetPath(id.Substring(IdPrefix.Length))) : null;

        /// <summary>Asks where to save a copy of the action (with what it holds) and saves it.</summary>
        internal static void Save(KAction action, string title)
        {
            var path = EditorUtility.SaveFilePanelInProject(Texts.T("Save action", "アクションを保存"), title, "asset",
                Texts.T("Pick it later from \"Saved actions\" when adding what to do.", "「何をするかを追加」の「保存したアクション」から使えます。"));
            if (string.IsNullOrEmpty(path)) return;
            EditorGUIUtility.PingObject(SaveAt(action, path));
        }

        /// <summary>Saves a copy of the action at path (without its scene references) and returns the asset.</summary>
        internal static TripwireActionSet SaveAt(KAction action, string path)
        {
            var copy = TripwireTriggerEditor.CloneOf(action);
            int dropped = 0;
            foreach (var a in TripwireModel.FlatActions(new[] { copy }))
                foreach (var arg in a.args)
                    dropped += arg.objects.RemoveAll(o => o != null && !EditorUtility.IsPersistent(o));
            var set = CreateInstance<TripwireActionSet>();
            set.actions.Add(copy);
            set.description = Texts.T("Saved from a trigger.", "トリガーから保存したアクションです。")
                              + (dropped > 0 ? Texts.T(" Drag the objects in again where it is used.", "使う場所で、対象のオブジェクトを入れ直してください。") : "");
            AssetDatabase.CreateAsset(set, path);
            AssetDatabase.SaveAssets();
            return set;
        }
    }
}
