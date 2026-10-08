using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Tripwire.Core;
using UnityEditor;
using UnityEngine;

namespace Tripwire.Editor
{
    // The trigger Inspector: dragging event and action cards to reorder them, also between events and between triggers.
    internal sealed partial class TripwireTriggerEditor
    {
        const string DragKey = "Tripwire.Card";

        /// <summary>What is being dragged: an item, the list it is in and the trigger that owns that list.</summary>
        sealed class DragPayload
        {
            public TripwireTrigger Owner;
            public IList List;
            /// <summary>The card grabbed; Items are all the cards moving (the selection, in list order), Item included.</summary>
            public object Item;
            public List<object> Items;
        }

        // ---------------- selection (Ctrl / Cmd + click on the ≡ grip) ----------------

        /// <summary>Selected cards, all from one list: dragging or the ⋯ menu of one of them acts on them all.</summary>
        static readonly List<object> selected = new List<object>();
        static IList selectedIn;

        static readonly Color SelectedColor = new Color(0.3f, 0.6f, 1f);

        static bool IsSelected(object item) => selected.Contains(item);

        static void ClearSelection()
        {
            selected.Clear();
            selectedIn = null;
        }

        static void ToggleSelected(IList list, object item)
        {
            if (!ReferenceEquals(selectedIn, list)) { ClearSelection(); selectedIn = list; }
            if (!selected.Remove(item)) selected.Add(item);
        }

        /// <summary>Whether the selection is in one of this trigger's lists (cards, or actions at any depth).</summary>
        bool SelectionHere()
        {
            if (selected.Count == 0 || selectedIn == null) return false;
            if (ReferenceEquals(selectedIn, t.events)) return true;
            return t.events.Any(e => ActionLists(e.actions).Any(l => ReferenceEquals(l, selectedIn)));
        }

        static IEnumerable<List<KAction>> ActionLists(List<KAction> list)
        {
            yield return list;
            foreach (var a in list.Where(x => x != null))
            {
                foreach (var l in ActionLists(a.thenActions)) yield return l;
                foreach (var l in ActionLists(a.elseActions)) yield return l;
            }
        }

        /// <summary>The selection in list order.</summary>
        List<object> SelectedInOrder() => selectedIn.Cast<object>().Where(x => selected.Contains(x)).ToList();

        void DeleteSelection() => Edit(() => { foreach (var x in SelectedInOrder()) selectedIn.Remove(x); ClearSelection(); }, T("Delete", "削除"));

        void DuplicateSelection() => Edit(() =>
        {
            var items = SelectedInOrder();
            int at = selectedIn.IndexOf(items[items.Count - 1]) + 1;
            foreach (var x in items) selectedIn.Insert(at++, CloneOf(x));
        }, T("Duplicate", "複製"));

        void CopySelection()
        {
            var items = SelectedInOrder();
            if (selectedIn is List<KEvent>) CopyAll(items.Cast<KEvent>().ToList());
            else CopyAll(items.Cast<KAction>().ToList());
        }

        /// <summary>Pastes after the selection, into the selection's list, when the clipboard holds that kind.</summary>
        bool PasteAfterSelection()
        {
            var items = SelectedInOrder();
            int at = selectedIn.IndexOf(items[items.Count - 1]) + 1;
            if (selectedIn is List<KEvent> events && CanPaste<KEvent>()) { Edit(() => events.InsertRange(at, PastedHere<KEvent>()), T("Paste", "貼り付け")); return true; }
            if (selectedIn is List<KAction> actions && CanPaste<KAction>()) { Edit(() => actions.InsertRange(at, PastedHere<KAction>()), T("Paste", "貼り付け")); return true; }
            return false;
        }

        /// <summary>
        /// Keys on selected cards (while no text field is being edited): Delete, Ctrl+D, Ctrl+C, Ctrl+V and Esc, through
        /// Unity's commands so they act here instead of on the GameObject.
        /// </summary>
        void HandleSelectionKeys()
        {
            var e = Event.current;
            if (!SelectionHere() || EditorGUIUtility.editingTextField) return;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { ClearSelection(); e.Use(); Repaint(); return; }
            if (e.type != EventType.ValidateCommand && e.type != EventType.ExecuteCommand) return;
            bool run = e.type == EventType.ExecuteCommand;
            switch (e.commandName)
            {
                case "Delete":
                case "SoftDelete": if (run) DeleteSelection(); break;
                case "Duplicate": if (run) DuplicateSelection(); break;
                case "Copy": if (run) CopySelection(); break;
                case "Paste": if (!(selectedIn is List<KEvent> ? CanPaste<KEvent>() : CanPaste<KAction>())) return; if (run) PasteAfterSelection(); break;
                default: return;
            }
            e.Use();
        }

        /// <summary>While cards are selected: how many, and what can be done with them.</summary>
        void SelectionBar()
        {
            if (!SelectionHere()) return;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label(T(selected.Count + " selected  (Delete, Ctrl+D duplicate, Ctrl+C / V, Esc)", selected.Count + " 個を選択中（Delete で削除、Ctrl+D で複製、Ctrl+C / V、Esc で解除）"), EditorStyles.wordWrappedMiniLabel, GUILayout.MinWidth(60));
                if (GUILayout.Button(T("Duplicate", "複製"), EditorStyles.miniButtonLeft, GUILayout.ExpandWidth(false))) DuplicateSelection();
                if (GUILayout.Button(T("Delete", "削除"), EditorStyles.miniButtonMid, GUILayout.ExpandWidth(false))) DeleteSelection();
                if (GUILayout.Button(T("Clear", "解除"), EditorStyles.miniButtonRight, GUILayout.ExpandWidth(false))) { ClearSelection(); Repaint(); }
            }
        }

        /// <summary>The selected cards of list in list order when item is one of them (and there are several), else just item.</summary>
        static List<TItem> SelectionOf<TItem>(List<TItem> list, TItem item) =>
            ReferenceEquals(selectedIn, list) && selected.Count > 1 && selected.Contains(item)
                ? list.Where(x => selected.Contains(x)).ToList()
                : new List<TItem> { item };

        static void SelectedFrame(Rect card, object item)
        {
            if (Event.current.type == EventType.Repaint && IsSelected(item)) Frame(new Rect(card.x + 2, card.y + 2, card.width - 4, card.height - 4), SelectedColor);
        }

        /// <summary>
        /// The card being dragged, or null. Card drags carry no objects or paths, so a later drag of objects or files
        /// (which may not reset the generic data) is never mistaken for an old card.
        /// </summary>
        static DragPayload Dragged =>
            DragAndDrop.objectReferences.Length == 0 && DragAndDrop.paths.Length == 0 ? DragAndDrop.GetGenericData(DragKey) as DragPayload : null;

        /// <summary>
        /// Cards may only move within one scene (or one prefab): references to objects of another scene are lost when
        /// it is saved, and a move leaves no copy behind.
        /// </summary>
        bool CanDropHere(DragPayload payload) =>
            payload.Owner != null && payload.Owner.gameObject.scene == t.gameObject.scene;

        /// <summary>Whether list is inside the dragged block (moving a block into its own contents would detach it).</summary>
        static bool IsInside(DragPayload payload, IList list) => payload.Items.Any(x => IsInside(x, list));

        static bool IsInside(object item, IList list)
        {
            if (!(item is KAction a)) return false;
            foreach (var inner in new[] { a.thenActions, a.elseActions })
            {
                if (ReferenceEquals(inner, list)) return true;
                foreach (var child in inner) if (IsInside(child, list)) return true;
            }
            return false;
        }

        /// <summary>For the drag check (DevDragCheck): screen rects of grips and cards, keyed by name. Null when off.</summary>
        internal static Dictionary<string, Rect> ScreenRects;

        static void RecordScreenRect(string key, Rect r)
        {
            if (ScreenRects == null || Event.current.type != EventType.Repaint) return;
            var min = GUIUtility.GUIToScreenPoint(r.min);
            ScreenRects[key] = new Rect(min, r.size);
        }

        /// <summary>Where a drop would land (drawn as a line), in this Inspector's GUI coordinates.</summary>
        Rect? dropLine;

        /// <summary>The ≡ grip at the start of a card header; dragging it starts moving the card.</summary>
        void DragHandle(IList list, object item, string label)
        {
            var r = GUILayoutUtility.GetRect(14, 20, GUILayout.Width(14));
            if (EditorApplication.isPlaying) return; // edits in play mode would be lost
            EditorGUIUtility.AddCursorRect(r, MouseCursor.Pan);
            RecordScreenRect("grip:" + label, r);
            int id = GUIUtility.GetControlID(FocusType.Passive);
            var ev = Event.current;
            switch (ev.GetTypeForControl(id))
            {
                case EventType.Repaint:
                    GUI.Label(r, new GUIContent("≡", T("Drag to move. Ctrl+click to select several.", "ドラッグで移動。Ctrl+クリックで複数選択。")), smallLabel);
                    break;
                case EventType.MouseDown:
                    if (!r.Contains(ev.mousePosition) || ev.button != 0) break;
                    if (ev.control || ev.command)
                    {
                        ToggleSelected(list, item); // select / unselect, no drag
                        GUIUtility.keyboardControl = 0; // keys go to the selection, not a text field
                        ev.Use();
                        Repaint();
                        break;
                    }
                    if (!IsSelected(item)) ClearSelection();
                    GUIUtility.hotControl = id;
                    ev.Use();
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id)
                    {
                        GUIUtility.hotControl = 0;
                        DragAndDrop.PrepareStartDrag();
                        DragAndDrop.objectReferences = new Object[0];
                        var moving = ReferenceEquals(selectedIn, list) && selected.Count > 1 && selected.Contains(item)
                            ? list.Cast<object>().Where(x => selected.Contains(x)).ToList()
                            : new List<object> { item };
                        DragAndDrop.SetGenericData(DragKey, new DragPayload { Owner = t, List = list, Item = item, Items = moving });
                        DragAndDrop.StartDrag(label);
                        ev.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; ev.Use(); }
                    break;
            }
        }

        /// <summary>
        /// Makes a drawn card a drop target for items of type <typeparamref name="TItem"/>: the upper half inserts before
        /// it, the lower half after it.
        /// </summary>
        void DropOnCard<TItem>(Rect card, List<TItem> list, int index)
        {
            RecordScreenRect("card:" + typeof(TItem).Name + ":" + index, card);
            var ev = Event.current;
            if ((ev.type != EventType.DragUpdated && ev.type != EventType.DragPerform) || !card.Contains(ev.mousePosition) || EditorApplication.isPlaying) return;
            var payload = Dragged;
            if (payload == null || !(payload.Item is TItem)) return;
            if (!CanDropHere(payload) || IsInside(payload, list)) { DragAndDrop.visualMode = DragAndDropVisualMode.Rejected; ev.Use(); return; }
            bool after = ev.mousePosition.y > card.center.y;
            int at = index + (after ? 1 : 0);
            DragAndDrop.visualMode = DragAndDropVisualMode.Move;
            dropLine = new Rect(card.x, after ? card.yMax : card.y - 2, card.width, 2);
            if (ev.type == EventType.DragPerform) Drop(payload, list, at);
            ev.Use();
        }

        /// <summary>Dropping an action on an event card (not on one of its actions) adds it at the end of that event.</summary>
        void DropOnEvent(Rect card, KEvent e)
        {
            var ev = Event.current;
            if ((ev.type != EventType.DragUpdated && ev.type != EventType.DragPerform) || !card.Contains(ev.mousePosition) || EditorApplication.isPlaying) return;
            var payload = Dragged;
            if (payload == null || !(payload.Item is KAction)) return;
            if (!CanDropHere(payload)) { DragAndDrop.visualMode = DragAndDropVisualMode.Rejected; ev.Use(); return; }
            DragAndDrop.visualMode = DragAndDropVisualMode.Move;
            dropLine = new Rect(card.x + 16, card.yMax - 4, card.width - 16, 2);
            if (ev.type == EventType.DragPerform) Drop(payload, e.actions, e.actions.Count);
            ev.Use();
        }

        void Drop(DragPayload payload, IList target, int at)
        {
            DragAndDrop.AcceptDrag();
            dropLine = null;
            Undo.RecordObject(payload.Owner, "Move Tripwire card");
            Undo.RecordObject(t, T("Move Card", "カードを移動"));
            MoveAll(payload.List, payload.Items, target, at);
            if (!ReferenceEquals(payload.List, target)) ClearSelection();
            DragAndDrop.SetGenericData(DragKey, null); // done: a later drag from outside must not move it again
            if (payload.Owner != t) Changed(payload.Owner);
            Changed();
            Repaint();
        }

        /// <summary>Moves several items of source (in their order) to position at of target (counted before the move).</summary>
        internal static bool MoveAll(IList source, IList<object> items, IList target, int at)
        {
            if (items.Count == 1) return Move(source, items[0], target, at);
            var moving = items.Where(x => source.Contains(x)).ToList();
            if (moving.Count == 0) return false;
            if (ReferenceEquals(source, target)) at -= moving.Count(x => source.IndexOf(x) < at);
            foreach (var x in moving) source.Remove(x);
            at = Mathf.Clamp(at, 0, target.Count);
            for (int k = 0; k < moving.Count; k++) target.Insert(at + k, moving[k]);
            return true;
        }

        /// <summary>
        /// Moves <paramref name="item"/> from <paramref name="source"/> to position <paramref name="at"/> of
        /// <paramref name="target"/> (counted before the move). Returns false when nothing changes.
        /// </summary>
        internal static bool Move(IList source, object item, IList target, int at)
        {
            int from = source.IndexOf(item);
            if (from < 0) return false;
            if (ReferenceEquals(source, target))
            {
                if (at == from || at == from + 1) return false; // dropped on itself
                if (at > from) at--;
            }
            source.RemoveAt(from);
            target.Insert(Mathf.Clamp(at, 0, target.Count), item);
            return true;
        }

        /// <summary>Draws the drop line while a card is dragged over this Inspector; clears it when the drag ends.</summary>
        void DrawDropLine()
        {
            var ev = Event.current;
            if (ev.type == EventType.DragExited || Dragged == null) dropLine = null;
            if (dropLine != null && ev.type == EventType.Repaint) EditorGUI.DrawRect(dropLine.Value, new Color(0.3f, 0.6f, 1f));
            if (Dragged != null && (ev.type == EventType.DragUpdated || ev.type == EventType.MouseMove)) Repaint();
        }
    }
}
