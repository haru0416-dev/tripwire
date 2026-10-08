using System;
using System.Collections.Generic;
using System.Linq;
using Tripwire.Core;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tripwire.Editor
{
    // The trigger Inspector: what every card and action row shares (copy / paste, notes, the ⋯ menu).
    internal sealed partial class TripwireTriggerEditor
    {
        // ---------------- copy / paste ----------------

        // One clipboard for all Tripwire Inspectors: copy on one trigger, paste on another.
        static object clip;

        /// <summary>
        /// A deep copy of trigger data: lists and nested data are copied, Unity objects stay the same references (scene
        /// references survive, which Unity's JSON round trip loses).
        /// </summary>
        internal static TItem CloneOf<TItem>(TItem item)
        {
            var copy = (TItem)DeepCopy(item);
            if (copy is KEvent e) e.id = ""; // a copy is another card (EnsureIds gives it its own id)
            return copy;
        }

        static object DeepCopy(object o)
        {
            if (o == null || o is Object || o is string || o.GetType().IsValueType) return o;
            if (o is System.Collections.IList list)
            {
                var copy = (System.Collections.IList)Activator.CreateInstance(o.GetType());
                foreach (var x in list) copy.Add(DeepCopy(x));
                return copy;
            }
            var result = Activator.CreateInstance(o.GetType());
            foreach (var f in o.GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
                f.SetValue(result, DeepCopy(f.GetValue(o)));
            return result;
        }

        static UnityEngine.SceneManagement.Scene clipScene;

        internal static void Copy<TItem>(TItem item) => CopyAll(new List<TItem> { item });

        /// <summary>Copies several items (selected cards), kept in their order.</summary>
        internal static void CopyAll<TItem>(List<TItem> items) => clip = items.Select(CloneOf).ToList();

        void CopyFromHere<TItem>(List<TItem> items)
        {
            CopyAll(items);
            clipScene = t.gameObject.scene;
        }

        /// <summary>Pasted copies; warns when they came from another scene (their object references won't be saved).</summary>
        List<TItem> PastedHere<TItem>()
        {
            if (clipScene.IsValid() && clipScene != t.gameObject.scene)
                Debug.LogWarning("[Tripwire] " + T("Pasted from another scene: references to that scene's objects are not saved. Assign them again.",
                                                  "別のシーンからの貼り付けです。そのシーンのオブジェクトへの参照は保存されないので、入れ直してください。"), t);
            return PastedAll<TItem>();
        }

        internal static bool CanPaste<TItem>() => clip is List<TItem>;

        /// <summary>Fresh copies of everything copied (one or several items).</summary>
        internal static List<TItem> PastedAll<TItem>() => ((List<TItem>)clip).Select(CloneOf).ToList();

        internal static TItem Pasted<TItem>() => PastedAll<TItem>()[0];

        /// <summary>A small "Paste" button next to an add button, when the clipboard holds that kind of item.</summary>
        void PasteButton<TItem>(List<TItem> list, float height)
        {
            if (!CanPaste<TItem>()) return;
            if (GUILayout.Button(new GUIContent(T("Paste", "貼り付け"), T("Add the copied item at the end.", "コピーしたものを最後に追加します。")), EditorStyles.miniButton, GUILayout.Height(height), GUILayout.ExpandWidth(false)))
                Edit(() => list.AddRange(PastedHere<TItem>()), T("Paste", "貼り付け"));
        }

        // ---------------- comments ----------------

        /// <summary>Items whose (still empty) note was just opened from the ⋯ menu.</summary>
        readonly HashSet<object> editingComment = new HashSet<object>();
        static GUIStyle commentStyle;

        /// <summary>A note under a header: shown when it has text or was just opened; emptying it hides it again.</summary>
        string CommentField(object owner, string comment)
        {
            if (string.IsNullOrEmpty(comment) && !editingComment.Contains(owner)) return comment;
            if (commentStyle == null)
            {
                commentStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = true, fontStyle = FontStyle.Italic };
                commentStyle.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.75f, 0.8f, 0.6f) : new Color(0.28f, 0.36f, 0.1f);
            }
            var updated = EditorGUILayout.TextArea(comment ?? "", commentStyle);
            if (string.IsNullOrEmpty(updated))
            {
                var r = GUILayoutUtility.GetLastRect();
                GUI.Label(new Rect(r.x + 4, r.y, r.width - 4, r.height), T("Note (empty it to remove)", "メモ（空にすると消えます）"), EditorStyles.centeredGreyMiniLabel);
            }
            else editingComment.Remove(owner);
            return updated;
        }

        /// <summary>The ⋯ menu: duplicate, copy, paste below, note, move up / down, delete.</summary>
        bool ItemMenu<TItem>(List<TItem> list, int i, string what)
        {
            var r = GUILayoutUtility.GetRect(new GUIContent("⋯"), rowButton, GUILayout.Width(24), GUILayout.Height(RowHeight));
            if (!GUI.Button(r, new GUIContent("⋯", T("More", "その他の操作")), rowButton)) return false;
            var menu = new GenericMenu();
            var item = list[i];
            // On a selected card, the menu acts on every selected card of this list (in list order).
            var group = SelectionOf(list, item);
            if (group.Count > 1)
            {
                string n = group.Count.ToString();
                menu.AddItem(new GUIContent(T("Duplicate the " + n + " selected", "選んだ " + n + " 個を複製")), false, () => Edit(() =>
                    list.InsertRange(list.IndexOf(group[group.Count - 1]) + 1, group.Select(CloneOf)), T("Duplicate", "複製")));
                menu.AddItem(new GUIContent(T("Copy the " + n + " selected", "選んだ " + n + " 個をコピー")), false, () => CopyFromHere(group));
                menu.AddItem(new GUIContent(T("Delete the " + n + " selected", "選んだ " + n + " 個を削除")), false, () => Edit(() =>
                {
                    foreach (var x in group) list.Remove(x);
                    ClearSelection();
                }, T("Delete", "削除")));
                menu.AddSeparator("");
                menu.AddItem(new GUIContent(T("Clear the selection", "選択を解除")), false, () => { ClearSelection(); Repaint(); });
                menu.DropDown(r);
                return true;
            }
            menu.AddItem(new GUIContent(T("Duplicate", "複製")), false, () => Edit(() => list.Insert(list.IndexOf(item) + 1, CloneOf(item)), T("Duplicate", "複製")));
            menu.AddItem(new GUIContent(T("Copy", "コピー")), false, () => CopyFromHere(new List<TItem> { item }));
            if (CanPaste<TItem>()) menu.AddItem(new GUIContent(T("Paste below", "この下に貼り付け")), false, () => Edit(() => list.InsertRange(list.IndexOf(item) + 1, PastedHere<TItem>()), T("Paste", "貼り付け")));
            else menu.AddDisabledItem(new GUIContent(T("Paste below", "この下に貼り付け")));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent(T("Add a note", "メモを付ける")), false, () => { editingComment.Add(item); Repaint(); });
            if (item is KAction action)
                menu.AddItem(new GUIContent(T("Save to reuse…", "保存して使い回す…")), false,
                    () => TripwireActionSet.Save(action, ActionCatalog.Get(action.actionId) is ActionSpec s ? Texts.ActionName(s) : action.actionId));
            menu.AddSeparator("");
            if (i > 0) menu.AddItem(new GUIContent(T("Move up", "上へ移動")), false, () => Edit(() => { int k = list.IndexOf(item); (list[k - 1], list[k]) = (list[k], list[k - 1]); }, T("Move Up", "上へ移動")));
            else menu.AddDisabledItem(new GUIContent(T("Move up", "上へ移動")));
            if (i < list.Count - 1) menu.AddItem(new GUIContent(T("Move down", "下へ移動")), false, () => Edit(() => { int k = list.IndexOf(item); (list[k + 1], list[k]) = (list[k], list[k + 1]); }, T("Move Down", "下へ移動")));
            else menu.AddDisabledItem(new GUIContent(T("Move down", "下へ移動")));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent(T("Delete this ", "この") + what + T("", "を削除")), false, () => Edit(() => list.Remove(item), T("Delete", "削除")));
            menu.DropDown(r);
            return true;
        }
    }
}
