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
    // The trigger Inspector: argument fields (constants, variables, objects, players) and variable pickers.
    internal sealed partial class TripwireTriggerEditor
    {
        void DrawParam(EventSpec eventSpec, ActionSpec spec, KAction a, int k)
        {
            var prm = spec.Params[k];
            var arg = a.args[k];
            var label = Texts.Param(prm.Name);

            if (prm.Choices != null)
            {
                var choices = prm.Name == "broadcast" ? Texts.BroadcastChoices : prm.Name == "where" ? Texts.WhereChoices : prm.Choices;
                arg.intValue = EditorGUILayout.Popup(label, arg.intValue, choices);
                return;
            }
            if (prm.VariableRef)
            {
                var listType = prm.Role == VariableRole.Item ? TripwireModel.VariableType(t, a.args[0].stringValue)
                             : spec.Special == ActionSpecial.GetRemoteVariable ? TripwireModel.RemoteType(a) : null;
                Func<KVariable, bool> filter = v => ActionRules.VariableFits(spec, prm, TripwireModel.VariableType(v), v.synced, listType);
                if (prm.Optional)
                {
                    // "Not used" first, then the fitting variables.
                    if (arg.stringValue == null) arg.stringValue = "";
                    var names = new[] { "" }.Concat(t.variables.Where(filter).Select(v => v.name)).ToList();
                    if (!names.Contains(arg.stringValue)) names.Add(arg.stringValue);
                    var labels = names.Select(n => n == "" ? T("(not used)", "（使わない）") : n).ToArray();
                    arg.stringValue = names[EditorGUILayout.Popup(label, names.IndexOf(arg.stringValue), labels)];
                    return;
                }
                arg.stringValue = VariablePopup(label, arg.stringValue, filter);
                return;
            }

            if (prm.RemoteVariableRef)
            {
                // The variables of the trigger picked above.
                var other = TripwireModel.RemoteTrigger(a);
                if (other == null) { EditorGUILayout.LabelField(label, T("(pick the trigger first)", "（先にほかのトリガーを入れてください）")); return; }
                var names = other.variables.Where(v => !v.temporary).Select(v => v.name).ToList(); // temporary ones are private there
                if (names.Count == 0) { EditorGUILayout.LabelField(label, T("(that trigger has no variables)", "（そのトリガーに変数がありません）")); return; }
                if (string.IsNullOrEmpty(arg.stringValue)) arg.stringValue = names[0];
                var shownNames = names.Contains(arg.stringValue) ? names : new[] { arg.stringValue }.Concat(names).ToList();
                var shownLabels = shownNames.Select(x => names.Contains(x) ? x : x + T(" (missing)", "（見つかりません）")).ToArray();
                arg.stringValue = shownNames[EditorGUILayout.Popup(label, shownNames.IndexOf(arg.stringValue), shownLabels)];
                return;
            }
            if (prm.TimerRef)
            {
                var timers = t.events.Where(x => x.eventId == EventCatalog.TimerId && !string.IsNullOrEmpty(x.name)).Select(x => x.name).Distinct().ToList();
                if (timers.Count == 0) { EditorGUILayout.LabelField(label, T("(name a Timer event first)", "（先にタイマーのイベントに名前を付けてください）")); return; }
                if (string.IsNullOrEmpty(arg.stringValue)) arg.stringValue = timers[0];
                // A name no timer has (renamed or removed) stays shown until another is picked.
                var shown = timers.Contains(arg.stringValue) ? timers : new[] { arg.stringValue }.Concat(timers).ToList();
                var labels = shown.Select(x => timers.Contains(x) ? x : x + T(" (missing)", "（見つかりません）")).ToArray();
                arg.stringValue = shown[EditorGUILayout.Popup(label, shown.IndexOf(arg.stringValue), labels)];
                return;
            }

            var type = TripwireModel.EffectiveType(t, a, prm);
            if (type == null)
            {
                EditorGUILayout.LabelField(label, T("(pick a variable first)", "（先に変数を選んでください）"));
                return;
            }
            if (spec.Special == ActionSpecial.SetVariable && type.IsArray)
            {
                WholeArrayParam(label, type, arg);
                return;
            }
            DrawTypedParam(label, eventSpec, type, arg, prm.Template, prm.Multiline);
        }

        /// <summary>Setting an array variable: dragged objects (object arrays) or another array variable, never one element.</summary>
        void WholeArrayParam(string label, ParamType type, KArg arg)
        {
            var options = new List<string>();
            var sources = new List<KArgSource>();
            var names = new List<string>();
            if (type.Kind == ValueKind.Object)
            {
                options.Add(T("These objects", "指定したオブジェクト"));
                sources.Add(KArgSource.Objects);
                names.Add(null);
            }
            foreach (var v in t.variables.Where(v => Assignable(type, v)))
            {
                options.Add(T("Variable: ", "変数: ") + v.name);
                sources.Add(KArgSource.Variable);
                names.Add(v.name);
            }
            if (options.Count == 0)
            {
                EditorGUILayout.LabelField(label, T("(add another " + Texts.TypeName(type) + " variable)", "（" + Texts.TypeName(type) + " の変数をもう1つ作ってください）"));
                return;
            }
            int idx = MatchOrKeep(options, sources, names, arg, i => sources[i] == arg.source && (arg.source != KArgSource.Variable || names[i] == arg.name));
            idx = EditorGUILayout.Popup(label, idx, options.ToArray());
            arg.source = sources[idx];
            arg.name = names[idx];
            if (arg.source == KArgSource.Objects) ObjectList(null, type, arg);
        }

        void DrawTypedParam(string label, EventSpec eventSpec, ParamType type, KArg arg, bool template = false, bool multiline = false)
        {
            switch (type.Kind)
            {
                case ValueKind.Object:
                    ObjectParam(label, eventSpec, type, arg);
                    break;
                case ValueKind.Player:
                    PlayerParam(label, eventSpec, arg);
                    break;
                default:
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PrefixLabel(label);
                        int indent = EditorGUI.indentLevel;
                        EditorGUI.indentLevel = 0;
                        ValueSourceField(type, arg, eventSpec, template, multiline);
                        EditorGUI.indentLevel = indent;
                    }
                    break;
            }
        }

        /// <summary>
        /// A constant field, with a switch to a fitting variable or the event's own value (a toggle's on/off,
        /// a slider's position) when those exist.
        /// </summary>
        void ValueSourceField(ParamType type, KArg arg, EventSpec eventSpec = null, bool template = false, bool multiline = false)
        {
            var sources = new List<KArgSource>();
            var labels = new List<string>();
            var names = new List<string>();
            if (UdonApi.HasEditableConstant(type)) { sources.Add(KArgSource.Constant); labels.Add(T("Value", "値")); names.Add(null); }
            if (eventSpec != null)
                foreach (var ep in eventSpec.Params.Where(p => (p.Type.Kind != ValueKind.Object && p.Type.Kind != ValueKind.Player && CodeGenerator.IsAssignable(type, p.Type))
                                                               || (IsText(type) && CodeGenerator.IsTextConvertible(p.Type))))
                {
                    sources.Add(KArgSource.EventParam);
                    labels.Add(Texts.EventValueName(eventSpec, ep.Name));
                    names.Add(ep.Name);
                }
            if (t.variables.Any(v => Assignable(type, v))) { sources.Add(KArgSource.Variable); labels.Add(T("Variable", "変数")); names.Add(null); }
            if (sources.Count == 0)
            {
                arg.source = KArgSource.Variable;
                EditorGUILayout.LabelField(T("(no fitting variable yet)", "（合う変数がまだありません）"));
                return;
            }

            int idx = 0;
            for (int i = 0; i < sources.Count; i++)
                if (sources[i] == arg.source && (arg.source != KArgSource.EventParam || names[i] == arg.name)) idx = i;
            if (sources.Count > 1)
                idx = EditorGUILayout.Popup(idx, labels.ToArray(), GUILayout.Width(Mathf.Max(56, labels.Max(l => l.Length) * 12)));
            arg.source = sources[idx];
            if (arg.source == KArgSource.EventParam) arg.name = names[idx];

            if (arg.source == KArgSource.Variable)
                arg.name = VariablePopupInline(arg.name, v => Assignable(type, v));
            else if (arg.source == KArgSource.Constant)
            {
                // Text shown to people, or a list of names: on several lines.
                if (multiline && IsText(type)) arg.stringValue = EditorGUILayout.TextArea(arg.stringValue ?? "", MultilineText, GUILayout.MinHeight(EditorGUIUtility.singleLineHeight));
                else ConstantField(type, arg);
                // Placeholder: how to put a value into a text shown to people (a variable's, else one any text has).
                var example = !template || !IsText(type) || !string.IsNullOrEmpty(arg.stringValue) ? null
                    : t.variables.Select(v => v.name).Concat(eventSpec != null ? eventSpec.Params.Select(x => x.Name) : Enumerable.Empty<string>())
                        .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n) && n.IndexOfAny(new[] { '{', '}' }) < 0) ?? T("playerCount", "プレイヤー数");
                if (example != null)
                {
                    var r = GUILayoutUtility.GetLastRect();
                    GUI.Label(new Rect(r.x + 4, r.y, r.width - 4, r.height), T("{" + example + "} inserts a value", "{" + example + "} で値を入れられます"), EditorStyles.centeredGreyMiniLabel);
                }
            }
        }

        static GUIStyle multilineText;
        static GUIStyle MultilineText => multilineText ??= new GUIStyle(EditorStyles.textArea) { wordWrap = true };

        bool Assignable(ParamType want, KVariable v)
        {
            var vt = TripwireModel.VariableType(v);
            return vt != null && (CodeGenerator.IsAssignable(want, vt) || (IsText(want) && CodeGenerator.IsTextConvertible(vt)));
        }

        /// <summary>Text parameters also take numbers, names... as text (and {name} placeholders in the constant).</summary>
        static bool IsText(ParamType t) => t != null && t.Kind == ValueKind.String && !t.IsArray;

        /// <summary>For target lists: an array variable (looped) or a single one of a compatible type.</summary>
        bool AssignableTarget(ParamType want, KVariable v)
        {
            var vt = TripwireModel.VariableType(v);
            if (vt == null) return false;
            if (!want.IsArray) return CodeGenerator.IsAssignable(want, vt);
            return CodeGenerator.IsAssignable(want.Element(), vt.IsArray ? vt.Element() : vt);
        }

        static bool VarIs(KVariable v, ValueKind kind)
        {
            var vt = TripwireModel.VariableType(v);
            return vt != null && !vt.IsArray && vt.Kind == kind;
        }

        static bool IsLiteralVar(KVariable v)
        {
            var vt = TripwireModel.VariableType(v);
            return vt != null && UdonApi.HasEditableConstant(vt);
        }

        // ---------------- constants, objects, players ----------------

        static readonly UnityEditor.IMGUI.Controls.AdvancedDropdownState apiDropdownState = new UnityEditor.IMGUI.Controls.AdvancedDropdownState();

        static void ConstantField(ParamType type, KArg arg)
        {
            switch (type.Kind)
            {
                case ValueKind.Vector2:
                    var v2 = EditorGUILayout.Vector2Field(GUIContent.none, new Vector2(arg.vector4Value.x, arg.vector4Value.y));
                    arg.vector4Value = new Vector4(v2.x, v2.y, 0, 0);
                    return;
                case ValueKind.Color:
                    arg.vector4Value = (Vector4)EditorGUILayout.ColorField(GUIContent.none, (Color)arg.vector4Value);
                    return;
                case ValueKind.Quaternion:
                    var euler = EditorGUILayout.Vector3Field(GUIContent.none, (Vector3)arg.vector4Value);
                    arg.vector4Value = new Vector4(euler.x, euler.y, euler.z, 0);
                    return;
                case ValueKind.Enum:
                    var enumType = TripwireModel.ResolveType(type.UnityType);
                    var names = UdonApi.EnumMembers(enumType);
                    if (names.Length == 0) { EditorGUILayout.LabelField(T("(unknown enum)", "（不明な選択肢）")); return; }
                    int idx = Math.Max(0, Array.IndexOf(names, arg.stringValue));
                    arg.stringValue = names[EditorGUILayout.Popup(idx, names)];
                    return;
                case ValueKind.Bool:
                    arg.boolValue = EditorGUILayout.Popup(arg.boolValue ? 0 : 1, Texts.Japanese ? new[] { "オン", "オフ" } : new[] { "On", "Off" }) == 0;
                    return;
                case ValueKind.Url:
                    arg.stringValue = EditorGUILayout.TextField(arg.stringValue);
                    if (string.IsNullOrEmpty(arg.stringValue))
                    {
                        var r = GUILayoutUtility.GetLastRect();
                        GUI.Label(new Rect(r.x + 4, r.y, r.width, r.height), "https://…", EditorStyles.centeredGreyMiniLabel);
                    }
                    return;
                default:
                    ConstantField(type.Kind, arg);
                    return;
            }
        }

        static void ConstantField(ValueKind kind, KArg arg)
        {
            switch (kind)
            {
                case ValueKind.Int: arg.intValue = EditorGUILayout.IntField(arg.intValue); break;
                case ValueKind.Float: arg.floatValue = EditorGUILayout.FloatField(arg.floatValue); break;
                case ValueKind.String: arg.stringValue = EditorGUILayout.TextField(arg.stringValue); break;
                case ValueKind.Vector3: arg.vectorValue = EditorGUILayout.Vector3Field(GUIContent.none, arg.vectorValue); break;
            }
        }

        void PlayerParam(string label, EventSpec eventSpec, KArg arg)
        {
            var options = new List<string> { T("Me", "自分") };
            var sources = new List<KArgSource> { KArgSource.LocalPlayer };
            var names = new List<string> { null };
            if (eventSpec != null)
                foreach (var ep in eventSpec.Params.Where(p => p.Type.Kind == ValueKind.Player))
                {
                    options.Add(Texts.EventValueName(eventSpec, ep.Name));
                    sources.Add(KArgSource.EventParam);
                    names.Add(ep.Name);
                }
            foreach (var v in t.variables.Where(v => VarIs(v, ValueKind.Player)))
            {
                options.Add(T("Variable: ", "変数: ") + v.name);
                sources.Add(KArgSource.Variable);
                names.Add(v.name);
            }
            int idx = MatchOrKeep(options, sources, names, arg, i => sources[i] == arg.source && (arg.source == KArgSource.LocalPlayer || names[i] == arg.name));
            idx = EditorGUILayout.Popup(label, idx, options.ToArray());
            arg.source = sources[idx];
            arg.name = names[idx];
        }

        void ObjectParam(string label, EventSpec eventSpec, ParamType type, KArg arg)
        {
            var options = new List<string> { T("These objects", "指定したオブジェクト") };
            var sources = new List<KArgSource> { KArgSource.Objects };
            var names = new List<string> { null };
            // "This object" works for the GameObject itself and for anything GetComponent can find on it.
            bool selfOk = type.IsComponent || type.UnityType == "UnityEngine.GameObject" || type.UnityType == "UnityEngine.Transform" || type.UnityType == ParamType.Behaviour;
            if (selfOk)
            {
                options.Add(T("This object", "このオブジェクト"));
                sources.Add(KArgSource.Self);
                names.Add(null);
            }
            if (eventSpec != null)
                foreach (var ep in eventSpec.Params.Where(p => p.Type.Kind == ValueKind.Object && (selfOk || p.Type.UnityType == type.UnityType)))
                {
                    options.Add(Texts.EventValueName(eventSpec, ep.Name));
                    sources.Add(KArgSource.EventParam);
                    names.Add(ep.Name);
                }
            foreach (var v in t.variables.Where(v => AssignableTarget(type, v)))
            {
                options.Add(T("Variable: ", "変数: ") + v.name);
                sources.Add(KArgSource.Variable);
                names.Add(v.name);
            }
            int idx = MatchOrKeep(options, sources, names, arg, i => sources[i] == arg.source && (arg.source != KArgSource.EventParam && arg.source != KArgSource.Variable || names[i] == arg.name));

            if (options.Count > 1)
            {
                idx = EditorGUILayout.Popup(label, idx, options.ToArray());
                arg.source = sources[idx];
                arg.name = names[idx];
                if (arg.source != KArgSource.Objects) return;
                ObjectList(null, type, arg);
            }
            else
            {
                arg.source = KArgSource.Objects;
                ObjectList(label, type, arg);
            }
        }

        /// <summary>One object field, or an editable list with a drop area for array types.</summary>
        void ObjectList(string label, ParamType type, KArg arg)
        {
            var want = TripwireModel.ResolveType(type.UnityType);
            var fieldType = want == null || want == typeof(UdonBehaviour) ? typeof(Object) : want;
            if (!type.IsArray)
            {
                // Drawing alone leaves the list as it is (a prefab instance would get an override just by being selected);
                // only a change to the field writes, and the first entry is the one a single-object parameter uses.
                var current = arg.objects.Count > 0 ? arg.objects[0] : null;
                bool wasEmpty = current == null; // decided before the field may change it this pass
                var picked = ObjectRow(label, current, fieldType, want, null);
                if (picked != current)
                {
                    arg.objects.Clear(); // entries left from an array this argument once was go with the change
                    arg.objects.Add(picked);
                }
                if (wasEmpty) FindButton(arg, want, all: false);
                return;
            }
            if (label != null) EditorGUILayout.LabelField(label);
            if (arg.objects.All(o => o == null)) FindButton(arg, want, all: true);
            for (int i = 0; i < arg.objects.Count; i++)
            {
                bool remove = false;
                arg.objects[i] = ObjectRow(null, arg.objects[i], fieldType, want, () => remove = true);
                if (remove) { arg.objects.RemoveAt(i); Changed(); GUIUtility.ExitGUI(); }
            }
            DropArea(arg, fieldType, want);
        }

        static Object ObjectRow(string label, Object current, Type fieldType, Type want, Action onRemove)
        {
            Object result;
            using (new EditorGUILayout.HorizontalScope())
            {
                result = label != null ? EditorGUILayout.ObjectField(label, current, fieldType, true) : EditorGUILayout.ObjectField(current, fieldType, true);
                if (onRemove != null && GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(22))) onRemove();
            }
            if (result != null && TripwireModel.Coerce(result, want) == null)
                EditorGUILayout.HelpBox(T(result.name + " has no " + (want != null ? want.Name : "matching component") + ".",
                                          result.name + " には " + (want != null ? want.Name : "対応するコンポーネント") + " がありません。"), MessageType.Warning);
            return result;
        }

        /// <summary>
        /// For an empty component field: fill it from this object and its children (the first match, or every match
        /// for a list), when there is any.
        /// </summary>
        void FindButton(KArg arg, Type want, bool all)
        {
            if (want == null || !typeof(Component).IsAssignableFrom(want) || want == typeof(UdonBehaviour) || EditorApplication.isPlaying) return;
            // Never Udon / U# / Tripwire parts: disabling the trigger's own behaviour would stop the trigger itself.
            var found = t.GetComponentsInChildren(want, true)
                .Where(c => !(c is TripwireTrigger) && !(c is UdonBehaviour) && !(c is UdonSharp.UdonSharpBehaviour) && (c.hideFlags & HideFlags.HideInInspector) == 0).ToList();
            if (found.Count == 0) return;
            var name = all ? T("Use the " + found.Count + " " + want.Name + " here", "ここにある " + want.Name + " を入れる（" + found.Count + " 個）")
                           : T("Use " + found[0].name + "'s " + want.Name, found[0].name + " の " + want.Name + " を入れる");
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(EditorGUIUtility.labelWidth);
                if (GUILayout.Button(new GUIContent(name, T("Found on this object or its children.", "このオブジェクトとその子から見つけました。")), EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                {
                    Edit(() =>
                    {
                        arg.objects.RemoveAll(o => o == null);
                        arg.objects.AddRange(all ? found.Cast<Object>() : found.Take(1).Cast<Object>());
                    });
                    GUIUtility.ExitGUI(); // the rows below change: this pass's layout no longer fits
                }
            }
        }

        static void DropArea(KArg arg, Type fieldType, Type want)
        {
            var rect = EditorGUI.IndentedRect(GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true)));
            GUI.Box(rect, T("Drag objects here (click to add an empty slot)", "ここへドラッグで追加（クリックで空欄を追加）"), EditorStyles.helpBox);
            RecordScreenRect("objects-area", rect);
            var ev = Event.current;
            if (!rect.Contains(ev.mousePosition)) return;
            if ((ev.type == EventType.DragUpdated || ev.type == EventType.DragPerform) && Dragged != null) return; // a card, not objects
            switch (ev.type)
            {
                case EventType.MouseDown:
                    arg.objects.Add(null);
                    GUI.changed = true;
                    ev.Use();
                    break;
                case EventType.DragUpdated:
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    ev.Use();
                    break;
                case EventType.DragPerform:
                    DragAndDrop.AcceptDrag();
                    foreach (var o in DragAndDrop.objectReferences)
                    {
                        if (o == null) continue;
                        var coerced = TripwireModel.Coerce(o, want);
                        arg.objects.Add(fieldType == typeof(Object) ? o : coerced ?? o);
                    }
                    GUI.changed = true;
                    ev.Use();
                    break;
            }
        }

        /// <summary>
        /// Picks the option matching the argument. When it names a variable or event value that is gone (a variable
        /// renamed in another trigger, deleted, or a value of another event), the name stays as a "(missing)" choice
        /// rather than the first option silently taking its place; the generator then reports it.
        /// </summary>
        int MatchOrKeep(List<string> options, List<KArgSource> sources, List<string> names, KArg arg, Func<int, bool> matches)
        {
            for (int i = 0; i < sources.Count; i++) if (matches(i)) return Last(i, sources, matches);
            if ((arg.source == KArgSource.Variable || arg.source == KArgSource.EventParam) && !string.IsNullOrEmpty(arg.name))
            {
                options.Insert(0, arg.name + T(" (missing)", "（見つかりません）"));
                sources.Insert(0, arg.source);
                names.Insert(0, arg.name);
            }
            return 0;
        }

        // The last matching option (as the loops this replaces picked it).
        static int Last(int first, List<KArgSource> sources, Func<int, bool> matches)
        {
            int idx = first;
            for (int i = first + 1; i < sources.Count; i++) if (matches(i)) idx = i;
            return idx;
        }

        // ---------------- variable pickers ----------------

        string VariablePopup(string label, string current, Func<KVariable, bool> filter)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(label);
                int indent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;
                var r = VariablePopupInline(current, filter);
                EditorGUI.indentLevel = indent;
                return r;
            }
        }

        string VariablePopupInline(string current, Func<KVariable, bool> filter)
        {
            var names = t.variables.Where(filter).Select(v => v.name).ToList();
            if (names.Count == 0)
            {
                EditorGUILayout.LabelField(new GUIContent(T("(no variable of this kind yet)", "（使える変数がまだありません）"), T("Add one with \"+ Variable\" at the top.", "上の「＋ 変数」で作れます。")));
                return current;
            }
            int idx = names.IndexOf(current);
            var display = names.ToList();
            if (idx < 0)
            {
                display.Insert(0, string.IsNullOrEmpty(current) ? "—" : current + T(" (missing)", "（見つかりません）"));
                idx = EditorGUILayout.Popup(0, display.ToArray());
                return idx == 0 ? current : display[idx];
            }
            idx = EditorGUILayout.Popup(idx, display.ToArray());
            return names[idx];
        }
    }
}
