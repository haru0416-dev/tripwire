using System;
using System.Collections.Generic;
using System.Linq;
using Tripwire.Core;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tripwire.Editor
{
    // The trigger Inspector: action rows and their catalog arguments.
    internal sealed partial class TripwireTriggerEditor
    {
        static KAction NewAction(string id)
        {
            var a = new KAction { actionId = id };
            ResetArgs(a);
            return a;
        }

        static void ResetArgs(KAction a)
        {
            // Conditions and nested actions belong to blocks: drop what the new kind of action doesn't have
            // (it would be invisible, and Undo brings it back).
            var spec = ActionCatalog.Get(a.actionId);
            if (spec?.HasConditions != true) a.conditions.Clear();
            if (spec?.HasElse != true) a.elseActions.Clear();
            if (spec?.HoldsActions != true) a.thenActions.Clear();
            a.args.Clear();
            if (spec == null) return;
            foreach (var prm in spec.Params)
            {
                var arg = new KArg();
                if (prm.Type != null && prm.Type.Kind == ValueKind.Object) arg.source = KArgSource.Objects;
                else if (prm.Type != null && prm.Type.Kind == ValueKind.Player) arg.source = KArgSource.LocalPlayer;
                SetConstant(arg, prm.Default);
                a.args.Add(arg);
            }
        }

        static void SetConstant(KArg arg, object value)
        {
            switch (value)
            {
                case bool b: arg.boolValue = b; break;
                case int n: arg.intValue = n; break;
                case float f: arg.floatValue = f; break;
                case string s: arg.stringValue = s; break;
                case float[] v when v.Length == 3: arg.vectorValue = new Vector3(v[0], v[1], v[2]); break;
            }
        }

        /// <summary>
        /// A list of action cards with an add button (an event's actions, or one side of an If block). number gives
        /// each action's number in the generator's numbering, which diagnostics refer to.
        /// </summary>
        void DrawActionList(int ei, EventSpec eventSpec, List<KAction> list, Func<KAction, int> number)
        {
            list.RemoveAll(x => x == null); // a SerializeReference list can hold nulls (unresolved types, hand edits)
            for (int li = 0; li < list.Count; li++)
            {
                using (var card = new EditorGUILayout.VerticalScope(innerCardStyle))
                {
                    DrawAction(ei, eventSpec, list, li, number);
                    if (li < list.Count)
                    {
                        DropOnCard(card.rect, list, li);
                        SelectedFrame(card.rect, list[li]);
                        var color = CategoryColor(ActionCatalog.Get(list[li].actionId)?.Category);
                        Stripe(card.rect, new Color(color.r, color.g, color.b, 0.75f));
                    }
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(16);
                var r = GUILayoutUtility.GetRect(new GUIContent("x"), EditorStyles.miniButton, GUILayout.Height(22), GUILayout.ExpandWidth(true));
                if (GUI.Button(r, T("+ Add what to do", "＋ 何をするかを追加"), EditorStyles.miniButton))
                {
                    var target = list;
                    PopupWindow.Show(r, new CatalogPicker(CatalogPicker.Actions(withSaved: true), Texts.ActionCategories.Concat(new[] { "Saved" }).ToArray(), id => Edit(() =>
                    {
                        // A saved set adds copies of its actions; anything else is a new catalog action.
                        var saved = TripwireActionSet.Of(id);
                        if (saved != null) target.AddRange(saved.actions.Where(x => x != null).Select(CloneOf));
                        else target.Add(NewAction(id));
                    }, T("Add Action", "アクションを追加"))));
                }
                PasteButton(list, 22);
            }
        }

        /// <summary>Blocks folded in this session (editor state, like cards' folding; not saved in the scene).</summary>
        static readonly HashSet<KAction> foldedBlocks = new HashSet<KAction>();

        /// <summary>The list and position of the action being drawn (for operations that insert steps before it).</summary>
        List<KAction> currentList;
        int currentIndex;

        void DrawAction(int ei, EventSpec eventSpec, List<KAction> list, int li, Func<KAction, int> number)
        {
            var a = list[li];
            int ai = number(a);
            currentList = list;
            currentIndex = li;
            var spec = ActionCatalog.Get(a.actionId);
            // Blocks (If, While, For Each...) fold like cards; one with a problem inside stays open.
            bool block = spec != null && (spec.HasConditions || ActionCatalog.IsBlock(spec.Id));
            var inside = block ? TripwireModel.FlatActions(a.thenActions).Concat(TripwireModel.FlatActions(a.elseActions)).ToList() : null;
            bool problemInside = block && inside.Any(x => generated.Diagnostics.Any(d => d.Event == ei && d.Action == number(x) && d.Severity == Severity.Error));
            bool folded = block && !problemInside && foldedBlocks.Contains(a);
            using (new EditorGUILayout.HorizontalScope())
            {
                var title = spec == null ? T("Unknown action: ", "不明なアクション: ") + a.actionId : Texts.ActionName(spec);
                DragHandle(list, a, title);
                if (block)
                {
                    var foldRect = GUILayoutUtility.GetRect(14, 20, GUILayout.Width(14));
                    bool hierarchy = EditorGUIUtility.hierarchyMode;
                    EditorGUIUtility.hierarchyMode = false;
                    bool open = EditorGUI.Foldout(foldRect, !folded, GUIContent.none, true);
                    EditorGUIUtility.hierarchyMode = hierarchy;
                    if (open == folded) { if (open) foldedBlocks.Remove(a); else foldedBlocks.Add(a); folded = !open; }
                }
                if (ActionIcon(spec) != null) IconSlot(ActionIcon(spec), CategoryColor(spec.Category));
                // MinWidth: a long title is clipped in a narrow Inspector instead of widening every card around it.
                var r = GUILayoutUtility.GetRect(new GUIContent(title), boldLabel, GUILayout.MinWidth(40), GUILayout.ExpandWidth(true));
                if (GUI.Button(r, new GUIContent(title, spec != null ? Texts.ActionDescription(spec) + T("\n(click to change)", "\n（クリックで変更）") : null), boldLabel))
                {
                    var action = a;
                    PopupWindow.Show(r, new CatalogPicker(CatalogPicker.Actions(), Texts.ActionCategories, id =>
                    {
                        // The actions inside a block are dropped when the new kind holds none.
                        var next = ActionCatalog.Get(id);
                        int lost = (next?.HoldsActions == true ? 0 : TripwireModel.FlatActions(action.thenActions).Count)
                                 + (next?.HasElse == true ? 0 : TripwireModel.FlatActions(action.elseActions).Count);
                        if (lost > 0 && !EditorUtility.DisplayDialog("Tripwire",
                                T("The " + lost + " action(s) inside are removed with this change. Undo brings them back.", "変えると、中の " + lost + " 個のアクションが消えます（Undo で戻せます）。"),
                                T("Change", "変える"), T("Cancel", "やめる"))) return;
                        Edit(() => { action.actionId = id; ResetArgs(action); }, T("Change Action", "アクションを変更"));
                    }));
                }
                if (folded)
                {
                    var summary = new GUIContent(T("→ " + inside.Count + " inside", "→ 中に " + inside.Count + " 個"));
                    GUILayout.Label(summary, smallLabel, GUILayout.MaxWidth(smallLabel.CalcSize(summary).x));
                }
                ItemMenu(list, li, T("action", "アクション"));
            }
            a.comment = CommentField(a, a.comment);
            if (spec == null || folded) return;
            if (spec.HasConditions)
            {
                // If [conditions] → Then actions / Else actions
                a.matchAny = DrawConditions(ei, ai, a.conditions, a.matchAny);
                DrawDiagnostics(d => d.Event == ei && d.Action == ai && d.Condition < 0);
                if (!spec.HasElse)
                {
                    // While: conditions, then the body.
                    BlockTag("Do");
                    DrawActionList(ei, eventSpec, a.thenActions, number);
                    return;
                }
                BlockTag("Then");
                DrawActionList(ei, eventSpec, a.thenActions, number);
                // An Otherwise holding just one If reads (and is generated) as "otherwise if".
                bool elseIf = a.elseActions.Count == 1 && a.elseActions[0]?.actionId == ActionCatalog.IfId;
                BlockTag(elseIf ? "Else If" : "Else");
                if (a.elseActions.Count == 0)
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(16);
                        if (GUILayout.Button(new GUIContent("+ Else If", T("Another condition to check when this one isn't met.", "この条件を満たさないときに、別の条件を調べます。")),
                                EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                        {
                            var ifBlock = a;
                            Edit(() => ifBlock.elseActions.Add(NewAction(ActionCatalog.IfId)), "+ Else If");
                        }
                    }
                DrawActionList(ei, eventSpec, a.elseActions, number);
                return;
            }
            if (spec.Special == ActionSpecial.Call)
            {
                if (spec.Id == ActionCatalog.ScriptCallId) DrawScriptCall(ei, eventSpec, a, ai);
                else DrawCall(ei, eventSpec, a, ai);
                DrawDiagnostics(d => d.Event == ei && d.Action == ai && d.Arg < 0);
                return;
            }
            while (a.args.Count < spec.Params.Length) a.args.Add(new KArg());

            for (int k = 0; k < spec.Params.Length; k++)
            {
                DrawParam(eventSpec, spec, a, k);
                int kk = k;
                DrawDiagnostics(d => d.Event == ei && d.Action == ai && d.Arg == kk);
            }
            DrawDiagnostics(d => d.Event == ei && d.Action == ai && d.Arg < 0);
            if (ActionCatalog.IsBlock(spec.Id))
            {
                BlockTag("Do");
                DrawActionList(ei, eventSpec, a.thenActions, number);
            }
        }
    }
}
