using System;
using System.Collections.Generic;
using System.Linq;
using Tripwire.Core;
using UnityEditor;
using UnityEngine;
using VRC.Udon;
using Object = UnityEngine.Object;

namespace Tripwire.Editor
{
    // The trigger Inspector: the variable list.
    internal sealed partial class TripwireTriggerEditor
    {
        void DrawVariables()
        {
            if (!variablesExpanded.HasValue) variablesExpanded = t.variables.Count > 0;
            if (HasDiagnostics(d => d.Variable >= 0)) variablesExpanded = true;
            using (new EditorGUILayout.HorizontalScope())
            {
                var title = T("Variables", "変数") + "  " + (t.variables.Count == 0 ? T("none", "なし") : t.variables.Count.ToString());
                variablesExpanded = EditorGUILayout.Foldout(variablesExpanded.Value, title, true, EditorStyles.foldoutHeader);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(T("+ Variable", "＋ 変数"), GUILayout.Width(80)))
                {
                    t.variables.Add(new KVariable { name = UniqueName(T("var", "変数"), t.variables.Select(x => x.name)), typeName = "System.Boolean" });
                    variablesExpanded = true;
                }
            }
            if (!variablesExpanded.Value) return;

            for (int i = 0; i < t.variables.Count; i++)
            {
                using (new EditorGUILayout.VerticalScope(IsCompact(TripwireModel.VariableType(t.variables[i])) ? slimCardStyle : cardStyle))
                    if (DrawVariable(i)) { Changed(); GUIUtility.ExitGUI(); } // the pass ends before the change check
            }
            if (t.variables.Any(v => v.synced) && (TripwireSettings.Detailed || t.continuousSync))
            {
                // How synced values travel: when they change (most gimmicks), or all the time, smoothed (moving things).
                int mode = EditorGUILayout.Popup(new GUIContent(T("Send synced", "同期の送り方"),
                        T("When changed: right after a change (switches, scores). All the time: several times a second, smoothed on arrival (positions that keep moving).",
                          "変えたときに送る: 値を変えた直後に送ります（スイッチや点数など）。常に送り続ける: 1 秒に何回も送り、受け取った側でなめらかにつなぎます（動き続ける位置など）。")),
                    t.continuousSync ? 1 : 0, new[] { new GUIContent(T("When changed", "変えたときに送る")), new GUIContent(T("All the time (smoothed)", "常に送り続ける（なめらか）")) });
                t.continuousSync = mode == 1;
            }
        }

        /// <summary>A single value with a short field (on/off, numbers, text, color): drawn in one row when there is room.</summary>
        static bool IsCompact(ParamType ptype) =>
            ptype != null && UdonApi.HasEditableConstant(ptype) && EditorGUIUtility.currentViewWidth >= 400
            && ptype.Kind != ValueKind.Vector2 && ptype.Kind != ValueKind.Vector3 && ptype.Kind != ValueKind.Quaternion;

        /// <summary>Returns true when the list changed (removed) and the GUI must restart.</summary>
        bool DrawVariable(int i)
        {
            var v = t.variables[i];
            var systemType = TripwireModel.ResolveType(TripwireModel.TypeNameOf(v));
            var ptype = TripwireModel.VariableType(v);
            bool canSync = systemType != null && ptype != null && ptype.Kind != ValueKind.Object && ptype.Kind != ValueKind.Player
                           && UdonNetworkTypes.CanSync(systemType);

            bool compact = IsCompact(ptype);
            using (new EditorGUILayout.HorizontalScope())
            {
                // Committed on Enter or leaving the field, then every use follows the new name (renaming on each
                // keystroke would pass through other variables' names and mix their uses).
                var renamed = EditorGUILayout.DelayedTextField(v.name);
                if (renamed != v.name)
                {
                    if (TripwireModel.IsEventValueName(t, renamed))
                        EditorWindow.focusedWindow?.ShowNotification(new GUIContent(T("'" + renamed + "' is an event value of this trigger: choose another name",
                                                                                       "「" + renamed + "」はこのトリガーのイベントの値の名前です。別の名前にしてください")));
                    else
                    {
                        TripwireModel.RenameVariable(t, v.name, renamed, TripwireCompiler.SceneTriggers());
                        v.name = renamed;
                    }
                }
                var typeLabel = ptype != null ? Texts.TypeName(ptype) : TripwireModel.TypeNameOf(v) + T(" (missing)", "（見つかりません）");
                var typeRect = EditorGUILayout.GetControlRect(false, GUILayout.Width(compact ? 104 : 160));
                // Hover: what it holds; the C# type only in "くわしく", where people use those names.
                var typeTip = (ptype != null ? Texts.TypeDescription(ptype) : "")
                              + (TripwireSettings.Detailed ? "\n" + TripwireModel.TypeNameOf(v) : "")
                              + T("\n(click to change)", "\n（クリックで変更）");
                if (GUI.Button(typeRect, new GUIContent(typeLabel, typeTip.TrimStart('\n')), EditorStyles.popup))
                {
                    var variable = v;
                    TypeMenu(typeRect, picked =>
                    {
                        int uses = TripwireModel.CountVariableUses(t, variable.name, TripwireCompiler.SceneTriggers());
                        if (uses > 0 && !EditorUtility.DisplayDialog("Tripwire",
                                T("Changing the type of '" + variable.name + "' resets its initial value and sync, and the " + uses + " place(s) using it may stop fitting. Undo brings it back.",
                                  "変数「" + variable.name + "」の型を変えると、最初の値と同期の設定が戻り、使っている " + uses + " か所が合わなくなることがあります（Undo で戻せます）。"),
                                T("Change", "変える"), T("Cancel", "やめる"))) return;
                        Edit(() =>
                        {
                            variable.typeName = picked.FullName;
                            variable.initial = new KArg();
                            variable.synced = false;
                            variable.temporary = false;
                            variable.saved = false;
                        }, T("Change Variable Type", "変数の型を変更"));
                    });
                }
                if (compact)
                {
                    // One row: the value and how it is kept beside the name (the common case: switches, counts).
                    using (new EditorGUILayout.HorizontalScope(GUILayout.MinWidth(56)))
                        ConstantField(ptype, v.initial);
                    KeepingPopup(v, canSync, CodeGenerator.CanBeTemporary(ptype));
                }
                if (GUILayout.Button(new GUIContent("×", T("Delete variable", "変数を削除")), EditorStyles.miniButton, GUILayout.Width(22)))
                {
                    int uses = TripwireModel.CountVariableUses(t, v.name, TripwireCompiler.SceneTriggers());
                    bool delete = uses == 0 || EditorUtility.DisplayDialog("Tripwire",
                        T("'" + v.name + "' is used in " + uses + " place(s). After deleting it, those need another variable. Undo brings it back.",
                          "変数「" + v.name + "」は " + uses + " か所で使われています。消すと、それらは変数の選び直しが必要になります（Undo で戻せます）。"),
                        T("Delete", "消す"), T("Cancel", "やめる"));
                    if (!delete) GUIUtility.ExitGUI(); // the dialog interrupted this pass's layout
                    Undo.SetCurrentGroupName(T("Delete Variable", "変数を削除"));
                    t.variables.RemoveAt(i);
                    return true;
                }
            }

            if (ptype != null && !compact)
            {
                if (UdonApi.HasEditableConstant(ptype))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(new GUIContent(T("Initial", "最初の値"), InitialTip(v)), GUILayout.Width(64));
                        ConstantField(ptype, v.initial);
                        GUILayout.Space(8);
                        KeepingPopup(v, canSync, CodeGenerator.CanBeTemporary(ptype));
                    }
                }
                else
                {
                    if (ptype.Kind == ValueKind.Object)
                    {
                        v.initial.source = KArgSource.Objects;
                        ObjectList(ptype.IsArray ? T("Initial contents", "最初の中身") : T("Initial", "最初の値"), ptype, v.initial);
                    }
                    if (canSync) v.synced = EditorGUILayout.ToggleLeft(T("Sync", "同期する"), v.synced);
                }
            }
            if (v.saved)
            {
                // The name in PlayerData: kept when the variable is renamed; the same name in other triggers is the same value.
                v.saveKey = EditorGUILayout.TextField(new GUIContent(T("Save name", "保存の名前"),
                    T("The name it is saved under. Variables saved under the same name, in any trigger of this world, share one value.",
                      "保存するときの名前です。このワールドのどのトリガーでも、同じ名前で保存した変数は同じ値になります。")), v.saveKey);
                if (string.IsNullOrWhiteSpace(v.saveKey)) v.saveKey = v.name;
            }
            int vi = i;
            DrawDiagnostics(d => d.Variable == vi);
            return false;
        }

        struct CommonType
        {
            public Type Type;
            public string En, Ja, HintEn, HintJa;
            public CommonType(Type type, string en, string ja, string hintEn, string hintJa) { Type = type; En = en; Ja = ja; HintEn = hintEn; HintJa = hintJa; }
        }

        static readonly CommonType[] CommonTypes =
        {
            new CommonType(typeof(bool), "On/Off", "オン/オフ", "open, lit", "開いている・ついている"),
            new CommonType(typeof(int), "Integer", "整数", "counts, scores", "回数・点数"),
            new CommonType(typeof(float), "Number", "小数", "time, volume", "時間・音量"),
            new CommonType(typeof(string), "Text", "文字", "names, messages", "名前・メッセージ"),
            new CommonType(typeof(Vector3), "Position", "位置", "x, y, z", "x, y, z"),
            new CommonType(typeof(Color), "Color", "色", "", ""),
            new CommonType(typeof(GameObject), "Object", "オブジェクト", "one", "1 つ"),
            new CommonType(typeof(GameObject[]), "Object list", "オブジェクトのリスト", "act on several", "まとめて操作"),
            new CommonType(typeof(VRC.SDKBase.VRCPlayerApi), "Player", "プレイヤー", "", ""),
        };

        static readonly UnityEditor.IMGUI.Controls.AdvancedDropdownState typeDropdownState = new UnityEditor.IMGUI.Controls.AdvancedDropdownState();

        static void TypeMenu(Rect rect, Action<Type> pick)
        {
            var menu = new GenericMenu();
            foreach (var c in CommonTypes)
            {
                var picked = c.Type;
                var hint = Texts.Japanese ? c.HintJa : c.HintEn;
                var label = (Texts.Japanese ? c.Ja : c.En) + (hint.Length > 0 ? "　（" + hint + "）" : "");
                menu.AddItem(new GUIContent(label), false, () => pick(picked));
            }
            menu.AddSeparator("");
            if (TripwireSettings.Detailed)
                menu.AddItem(new GUIContent(T("Other types… (advanced)", "その他の型…（上級）")), false, () => new VariableTypeDropdown(typeDropdownState, pick).Show(rect));
            else
                menu.AddDisabledItem(new GUIContent(T("Other types: switch to Detailed", "その他の型は「くわしく」で選べます")));
            menu.DropDown(rect);
        }

        /// <summary>The hover text of "最初の値": when the variable has it, which depends on how it is kept.</summary>
        static string InitialTip(KVariable v) =>
            v.temporary ? T("Each time an event uses it, it starts from this value.", "イベントが動くたびに、この値から始まります。")
            : v.saved ? T("The value for players who come for the first time. Those who have been here get what was saved for them.",
                          "初めて来た人の値です。前に来たことのある人には、保存しておいた値が戻ります。")
            : v.synced ? T("The value when the world is loaded. Players who join later get the current value instead.",
                           "ワールドを読み込んだときの値です。あとから来た人には、この値ではなく、そのときの値が届きます。")
            : T("The value when the world is loaded, on each player's screen. Actions change it from there.",
                "ワールドを読み込んだときの値です。各プレイヤーの画面でこの値から始まり、アクションで変わります。");

        static string UniqueName(string stem, IEnumerable<string> taken)
        {
            var set = new HashSet<string>(taken);
            for (int n = 1; ; n++)
                if (!set.Contains(stem + n)) return stem + n;
        }

        /// <summary>
        /// How a variable keeps its value: as usual, synced to everyone, temporary (each event starts afresh), or saved
        /// for each player (given back when they come again).
        /// </summary>
        void KeepingPopup(KVariable v, bool canSync, bool canBeTemporary)
        {
            var modes = new List<(string label, string tip, bool synced, bool temporary, bool saved)>
            {
                (T("Not synced", "同期しない"), T("Each player has a value of their own, kept until it is changed.", "プレイヤーごとに別の値を持ちます。変えるまでその値のままです。"), false, false, false),
            };
            if (canSync) modes.Add((T("Synced", "同期する"), T("Shared: players who join later get the same state.", "全員に届き、あとから来た人にも同じ値が届きます。"), true, false, false));
            if (canBeTemporary) modes.Add((T("Temporary", "一時的"), T("Starts from its initial value every time an event uses it (a local variable).", "イベントが動くたびに最初の値から始まります（ほかのトリガーからは見えません）。"), false, true, false));
            if (CodeGenerator.SavedAs(TripwireModel.VariableType(v)) != null)
                modes.Add((T("Saved", "保存する"), T("Each player's own value, saved (VRChat's PlayerData) and given back when they come again.", "その人の値を保存し、次に来たときに戻します（VRChat の PlayerData）。"), false, false, true));
            if (modes.Count == 1) { v.synced = v.temporary = v.saved = false; return; }
            int now = Math.Max(0, modes.FindIndex(m => m.synced == v.synced && m.temporary == v.temporary && m.saved == v.saved));
            int picked = EditorGUILayout.Popup(now, modes.Select(m => new GUIContent(m.label, m.tip)).ToArray(), GUILayout.Width(92));
            v.synced = modes[picked].synced;
            v.temporary = modes[picked].temporary;
            v.saved = modes[picked].saved;
            // Saved under the name it has now, so renaming the variable later keeps what players saved.
            if (v.saved && string.IsNullOrEmpty(v.saveKey)) v.saveKey = v.name;
        }
    }
}
