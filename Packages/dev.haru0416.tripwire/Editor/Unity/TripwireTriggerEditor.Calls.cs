using System;
using System.Collections.Generic;
using System.Linq;
using Tripwire.Core;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tripwire.Editor
{
    // The trigger Inspector: Udon API calls and other U# scripts (with asset presets).
    internal sealed partial class TripwireTriggerEditor
    {
        void DrawCall(int ei, EventSpec eventSpec, KAction a, int ai)
        {
            var c = UdonApi.Get(a.method);
            var rect = EditorGUI.PrefixLabel(EditorGUILayout.GetControlRect(), new GUIContent(T("Call", "呼び出すもの")));
            var title = c != null ? c.Display() : string.IsNullOrEmpty(a.method) ? T("Choose…", "選んでください…") : T("Not exposed: ", "公開されていません: ") + a.method;
            if (GUI.Button(rect, new GUIContent(title, c != null ? c.UdonName : null), EditorStyles.popup))
            {
                var action = a;
                new UdonApiDropdown(apiDropdownState, picked => Edit(() =>
                {
                    action.method = picked.UdonName;
                    action.resultVariable = "";
                    ResetCallArgs(action, picked);
                })).Show(rect);
            }
            if (c == null) return;
            DrawCallArgs(ei, eventSpec, a, ai, c, 0);
        }

        /// <summary>Inputs of a chosen member from <paramref name="first"/> on ([instance,] then parameters), and where the result goes.</summary>
        void DrawCallArgs(int ei, EventSpec eventSpec, KAction a, int ai, CallSpec c, int first, AssetPresets.Step preset = null)
        {
            var types = TripwireModel.CallArgTypes(c);
            while (a.args.Count < types.Count) a.args.Add(new KArg());
            for (int k = first; k < types.Count; k++)
            {
                int pi = k - (c.Instance != null ? 1 : 0);
                var label = c.Instance != null && k == 0 ? T("Target", "対象") : ObjectNames.NicifyVariableName(c.Params[pi].Name);
                if (preset != null && pi >= 0 && preset.Labels.TryGetValue(pi, out var named))
                {
                    if (named == null) continue; // an internal parameter: the preset's default stays
                    label = T(named.Value.En, named.Value.Ja);
                }
                DrawTypedParam(label, eventSpec, types[k], a.args[k]);
                int kk = k;
                DrawDiagnostics(d => d.Event == ei && d.Action == ai && d.Arg == kk);
            }

            if (c.Returns != null)
            {
                var names = t.variables
                    .Where(v => { var vt = TripwireModel.VariableType(v); return vt != null && CodeGenerator.IsAssignable(vt, c.Returns); })
                    .Select(v => v.name).ToList();
                if (c.Kind == CallKind.Method) names.Insert(0, "");
                var display = names.Select(n => n == "" ? T("(don't keep)", "（使わない）") : n).ToList();
                int idx = Math.Max(0, names.IndexOf(a.resultVariable ?? ""));
                var label = T("Store result in", "結果を入れる変数");
                if (names.Count == 0)
                    EditorGUILayout.LabelField(label, T("(add a " + Texts.TypeName(c.Returns) + " variable)", "（" + Texts.TypeName(c.Returns) + " の変数を作ってください）"));
                else
                    a.resultVariable = names[EditorGUILayout.Popup(label, idx, display.ToArray())];
            }
        }

        // ---------------- other U# scripts ----------------

        void DrawScriptCall(int ei, EventSpec eventSpec, KAction a, int ai)
        {
            var c = UdonSharpApi.Get(a.method);
            if (a.args.Count == 0) a.args.Add(new KArg { source = KArgSource.Objects });
            var target = a.args[0];
            target.source = KArgSource.Objects;
            ObjectList(T("Target", "対象"), ParamType.Objects(c != null ? c.DeclaringType : "UnityEngine.GameObject"), target);
            int ti = 0;
            DrawDiagnostics(d => d.Event == ei && d.Action == ai && d.Arg == ti);

            var first = target.objects.FirstOrDefault(o => o != null);
            var found = first != null ? UdonSharpApi.ScriptsOn(first) : new List<Type>();
            var scripts = found.Where(UdonSharpApi.IsReachable).ToList();
            if (found.Count > scripts.Count)
                GUILayout.Label(T("Some scripts here are in an assembly definition that is not auto-referenced, so triggers cannot use them: ",
                                  "使えないスクリプトがあります（アセンブリ定義の Auto Referenced がオフのため。オンにすると使えます）: ")
                                + string.Join(", ", found.Except(scripts).Select(x => x.Name)), captionLabel);
            if (scripts.Count == 0)
            {
                GUILayout.Label(T("Put in an object that has a U# script (a gimmick, a video player...).",
                                  "UdonSharp スクリプト（ギミックや動画プレイヤーなど）が付いたオブジェクトを入れてください。"), captionLabel);
                return;
            }

            var current = c != null ? TripwireModel.ResolveType(c.DeclaringType) : null;
            var cls = current != null && scripts.Contains(current) ? current : scripts[0];
            if (scripts.Count > 1)
                cls = scripts[EditorGUILayout.Popup(T("Script", "スクリプト"), scripts.IndexOf(cls), scripts.Select(x => x.Name).ToArray())];

            var members = UdonSharpApi.Members(cls);
            if (members.Count == 0)
            {
                GUILayout.Label(T("This script has nothing a trigger can use.", "このスクリプトには、トリガーから使えるものがありません。"), captionLabel);
                return;
            }
            // Known assets: their common operations first, every member in a submenu.
            var ops = PresetResolver.Operations(cls);
            var opLabels = ops.Select(o => T(o.En, o.Ja).Replace("/", "／")).ToList(); // '/' would open a submenu
            string allPrefix = ops.Count > 0 ? T("All members/", "すべての項目/") : "";
            int idx = c != null ? members.ToList().FindIndex(m => m.UdonName == c.UdonName) : -1;
            var labels = opLabels.Concat(members.Select(m => allPrefix + MemberLabel(m).Replace("/", "／"))).ToList();
            int shown = idx < 0 ? -1 : ops.Count + idx;
            if (shown >= 0 && ops.Count > 0)
            {
                // Show the operation's name when this action is its last step.
                var inList = currentList;
                int at0 = currentIndex;
                int opIdx = ops.FindIndex(o => IsCompleteOperation(cls, o, inList, at0));
                if (opIdx >= 0) shown = opIdx;
            }
            if (shown < 0) labels.Insert(0, T("Choose…", "選んでください…"));
            // Long lists (a script with many members) open with a search field.
            int picked = labels.Count > 15
                ? ChoiceDropdown.Layout(T("Use", "使うもの"), shown < 0 ? 0 : shown, labels.ToArray(), EditorWindow.focusedWindow)
                : EditorGUILayout.Popup(T("Use", "使うもの"), shown < 0 ? 0 : shown, labels.ToArray());
            if (shown < 0) picked--;
            if (picked >= 0 && picked < ops.Count && picked != shown)
            {
                var op = ops[picked];
                var targets = new List<Object>(target.objects);
                var list = currentList;
                var at = currentIndex;
                EditorApplication.delayCall += () => Edit(() =>
                {
                    var added = PresetResolver.Actions(cls, op, targets);
                    list[at] = added[added.Count - 1];
                    list.InsertRange(at, added.Take(added.Count - 1));
                });
                return;
            }
            if (picked >= ops.Count) picked -= ops.Count;
            else if (picked >= 0) picked = idx; // an operation's own entry: nothing changed
            if (picked >= 0 && (c == null || members[picked].UdonName != c.UdonName))
            {
                var keep = target;
                a.method = members[picked].UdonName;
                a.resultVariable = "";
                ResetCallArgs(a, members[picked]);
                a.args[0] = keep; // the target objects stay
                c = members[picked];
            }
            if (c == null || cls != current && current != null) return;
            var presetOp = ops.FirstOrDefault(o => PresetResolver.Resolve(cls, o).Last().UdonName == c.UdonName); // labels only
            DrawCallArgs(ei, eventSpec, a, ai, c, 1, presetOp?.Steps.Last());
        }

        /// <summary>
        /// Whether the action at <paramref name="ai"/> is the last step of the operation with its earlier steps right
        /// before it (an owner-only call whose "become owner" step was removed is shown as the bare member).
        /// </summary>
        static bool IsCompleteOperation(Type cls, AssetPresets.Operation op, List<KAction> actions, int ai)
        {
            var calls = PresetResolver.Resolve(cls, op);
            int first = ai - (calls.Count - 1);
            if (first < 0) return false;
            var targets = actions[ai].args.Count > 0 ? actions[ai].args[0].objects : null;
            for (int j = 0; j < calls.Count; j++)
            {
                var a = actions[first + j];
                if (a.actionId != ActionCatalog.ScriptCallId || a.method != calls[j].UdonName) return false;
                if (j < calls.Count - 1 && (a.args.Count == 0 || targets == null || !a.args[0].objects.SequenceEqual(targets))) return false;
            }
            return true;
        }

        static string MemberLabel(CallSpec c)
        {
            switch (c.Kind)
            {
                case CallKind.Get: return T("Read ", "") + c.Member + T("", " を読む") + "  (" + Texts.TypeName(c.Returns) + ")";
                case CallKind.Set: return T("Change ", "") + c.Member + T("", " を変える") + "  (" + Texts.TypeName(c.Params[0].Type) + ")";
                default:
                    return c.Member + "(" + string.Join(", ", c.Params.Select(p => Texts.TypeName(p.Type) + " " + p.Name)) + ")"
                           + (c.Returns != null ? " → " + Texts.TypeName(c.Returns) : "");
            }
        }

        static void ResetCallArgs(KAction a, CallSpec c)
        {
            a.args.Clear();
            foreach (var type in TripwireModel.CallArgTypes(c))
            {
                var arg = new KArg();
                if (type.Kind == ValueKind.Object) arg.source = KArgSource.Objects;
                else if (type.Kind == ValueKind.Player) arg.source = KArgSource.LocalPlayer;
                else if (type.Kind == ValueKind.Color) arg.vector4Value = Vector4.one;
                else if (type.Kind == ValueKind.Enum)
                {
                    var enumType = TripwireModel.ResolveType(type.UnityType);
                    var names = UdonApi.EnumMembers(enumType);
                    arg.stringValue = names.Length > 0 ? names[0] : "";
                }
                a.args.Add(arg);
            }
        }
    }
}
