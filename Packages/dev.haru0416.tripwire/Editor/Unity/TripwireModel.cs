using System;
using System.Collections.Generic;
using System.Linq;
using Tripwire.Core;
using UnityEngine;
using UnityEditor;
using VRC.Udon;
using Object = UnityEngine.Object;

namespace Tripwire.Editor
{
    /// <summary>Converts the serialized <see cref="TripwireTrigger"/> into the Core model, and resolves its references.</summary>
    internal static class TripwireModel
    {
        // KValueType's first five values, for variables saved before typeName existed.
        static readonly string[] LegacyTypeNames = { "System.Boolean", "System.Int32", "System.Single", "System.String", "UnityEngine.Vector3" };

        /// <summary>The variable's C# type name (migrating data saved with the old basic-type enum).</summary>
        public static string TypeNameOf(KVariable v) =>
            !string.IsNullOrEmpty(v.typeName) ? v.typeName : LegacyTypeNames[Math.Max(0, Math.Min((int)v.type, LegacyTypeNames.Length - 1))];

        /// <summary>The variable's type for the generator, or null when the type is unknown or not usable in Udon.</summary>
        public static ParamType VariableType(KVariable v)
        {
            var type = ResolveType(TypeNameOf(v));
            return type == null ? null : UdonApi.VariableParamType(type);
        }

        public static ParamType VariableType(TripwireTrigger t, string name)
        {
            var v = t.variables.FirstOrDefault(x => x.name == name);
            return v == null ? null : VariableType(v);
        }

        public static TriggerProgram ToProgram(TripwireTrigger t)
        {
            var p = new TriggerProgram { ContinuousSync = t.continuousSync };
            foreach (var v in t.variables)
            {
                var systemType = ResolveType(TypeNameOf(v));
                var type = systemType == null ? null : UdonApi.VariableParamType(systemType);
                var decl = new VariableDecl { Name = v.name, Synced = v.synced, Type = type ?? ParamType.OtherType(""), External = SetFromOutside(t, v.name), Temporary = v.temporary,
                    SaveKey = v.saved ? (string.IsNullOrEmpty(v.saveKey) ? v.name : v.saveKey) : null,
                    Interpolate = t.continuousSync && v.synced && SmoothedKind(type) && VRC.Udon.UdonNetworkTypes.CanSyncLinear(systemType) };
                // Object variables get their objects through a binding; other single values start from a constant.
                if (type != null && type.Kind != ValueKind.Object && !type.IsArray) decl.Initial = ConstOf(type, v.initial);
                p.Variables.Add(decl);
            }

            foreach (var e in t.events)
            {
                var block = new EventBlock
                {
                    EventId = e.eventId,
                    Name = e.name,
                    Broadcast = (Broadcast)(int)e.broadcast,
                    DelaySeconds = e.delaySeconds,
                    PlayerFilter = (PlayerFilter)(int)e.playerFilter,
                    Gate = (Gate)(int)e.gate,
                    GateNames = e.gateList != null ? e.gateList.names.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()).Distinct().ToList() : null,
                    InteractText = e.interactText,
                    HasUiSource = UiWiring.UiOf(e, EventCatalog.Get(e.eventId)) != null,
                    MatchAny = e.conditionsMatchAny,
                    Listen = EventCatalog.Get(e.eventId)?.Shape == EventShape.Listen ? ListenOf(e) : null,
                    Timer = EventCatalog.Get(e.eventId)?.Shape == EventShape.Timer
                        ? new TimerSpec { MinSeconds = e.timerMin, MaxSeconds = Math.Max(e.timerMin, e.timerMax), Repeat = e.timerRepeat, AutoStart = e.timerAutoStart }
                        : null,
                };
                foreach (var c in e.conditions) block.Conditions.Add(ToCondition(t, c));
                foreach (var a in e.actions)
                    block.Actions.Add(ToCall(t, a));
                p.Events.Add(block);
            }
            return p;
        }


        /// <summary>The script type a listen event's registration id names ("usharp:Type::m:Method(...)").</summary>
        public static Type ListenScriptType(KEvent e)
        {
            return UdonSharpApi.TypeOfId(e.listenMethod);
        }

        static ListenSpec ListenOf(KEvent e)
        {
            var l = new ListenSpec();
            if (string.IsNullOrEmpty(e.listenMethod)) return l; // the user lists the trigger in the script's Inspector
            var type = ListenScriptType(e);
            var r = UdonSharpApi.GetRegistration(type, e.listenMethod);
            l.RegisterMethod = r?.Method ?? e.listenMethod; // unknown method: the generator still reports a missing target / compile error
            if (r == null) return l;
            l.TargetType = r.DeclaringType;
            var script = type != null ? (e.listenTarget as GameObject ?? (e.listenTarget as Component)?.gameObject)?.GetComponent(type) : null;
            l.HasTarget = script != null;
            l.Params.AddRange(r.Params);
            l.SelfIndex = r.SelfIndex;
            // Only a public parameterless method of the script (left over from a preset on another script otherwise).
            l.Prepare = !string.IsNullOrEmpty(e.listenPrepare) && type?.GetMethod(e.listenPrepare, Type.EmptyTypes) is System.Reflection.MethodInfo pm && pm.IsPublic
                ? e.listenPrepare : null;
            l.PassCount = r.PassCount;
            // Dedupes identical registrations within one generation; never written into the code.
            var key = new System.Text.StringBuilder().Append(script != null ? script.GetInstanceID() : 0).Append('|').Append(r.Id).Append('|').Append(l.Prepare);
            for (int i = 0; i < r.Params.Count; i++)
            {
                if (i == r.SelfIndex) { l.Args.Add(null); continue; }
                var arg = i < e.listenArgs.Count ? e.listenArgs[i] : new KArg();
                var value = TypedArg(r.Params[i].Type, arg);
                l.Args.Add(value);
                key.Append('|').Append(arg.source).Append(':').Append(arg.source == KArgSource.Variable ? arg.name : Convert.ToString(value.Constant, System.Globalization.CultureInfo.InvariantCulture));
            }
            l.RegistrationKey = key.ToString();
            return l;
        }

        static Condition ToCondition(TripwireTrigger t, KCondition c)
        {
            var type = VariableType(t, c.variable);
            return new Condition { Variable = c.variable, Op = (CompareOp)(int)c.op, Value = type != null ? ValueArg(c.value, type) : null, Negate = c.negate };
        }

        /// <summary>
        /// Points every use of a variable at its new name: value sources, the variable of Set / Toggle / Add / Random and
        /// the like, conditions, "On Variable Changed" blocks, {name} in texts, registration arguments, and other
        /// triggers' "Set / Read Another Trigger's Variable" aimed at this one. Call before the variable itself takes the new name.
        /// Nothing changes when another variable already has either name: their uses would mix. Returns whether it renamed.
        /// </summary>
        public static bool RenameVariable(TripwireTrigger t, string from, string to, IEnumerable<TripwireTrigger> others)
        {
            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to) || from == to) return false;
            if (t.variables.Count(v => v.name == from) != 1 || t.variables.Any(v => v.name == to)) return false;
            if (IsEventValueName(t, to)) return false;
            VariableUses(t, from, to, others);
            return true;
        }

        /// <summary>How many places use the variable (in t, and other triggers' remote actions aimed at it).</summary>
        public static int CountVariableUses(TripwireTrigger t, string name, IEnumerable<TripwireTrigger> others) => VariableUses(t, name, null, others);

        /// <summary>Every use of variable <paramref name="from"/>: counted, and renamed to <paramref name="to"/> unless it is null.</summary>
        static int VariableUses(TripwireTrigger t, string from, string to, IEnumerable<TripwireTrigger> others)
        {
            int n = 0;
            bool write = to != null;
            void Arg(KArg a) { if (a != null && a.source == KArgSource.Variable && a.name == from) { n++; if (write) a.name = to; } }
            void Conditions(List<KCondition> list)
            {
                if (list == null) return;
                foreach (var c in list) { if (c == null) continue; if (c.variable == from) { n++; if (write) c.variable = to; } Arg(c.value); }
            }
            foreach (var e in t.events)
            {
                if (e == null) continue;
                if (e.eventId == EventCatalog.VariableChangedId && e.name == from) { n++; if (write) e.name = to; }
                Conditions(e.conditions);
                foreach (var a in e.listenArgs) Arg(a);
                foreach (var a in FlatActions(e.actions))
                {
                    Conditions(a.conditions);
                    if (a.resultVariable == from) { n++; if (write) a.resultVariable = to; } // Call: the variable the result goes into
                    var spec = ActionCatalog.Get(a.actionId);
                    for (int i = 0; i < a.args.Count; i++)
                    {
                        var arg = a.args[i];
                        Arg(arg);
                        var prm = spec != null && i < spec.Params.Length ? spec.Params[i] : null;
                        if (prm == null || arg == null) continue;
                        if (prm.VariableRef && arg.stringValue == from) { n++; if (write) arg.stringValue = to; }
                        if (prm.Template && RenameInTemplate(arg.stringValue, from, "\u0001") != arg.stringValue) { n++; if (write) arg.stringValue = RenameInTemplate(arg.stringValue, from, to); }
                    }
                    // A call reporting back to this trigger that writes one of its variables by name (VRCTween's variableName).
                    var call = a.actionId == ActionCatalog.CallId ? UdonApi.Get(a.method) : null;
                    if (call != null)
                    {
                        int first = call.Instance != null ? 1 : 0;
                        bool backHere = call.Params.Where((x, i) => x.Type.IsBehaviour && first + i < a.args.Count && a.args[first + i]?.source == KArgSource.Self).Any();
                        for (int i = 0; i < call.Params.Count && backHere; i++)
                        {
                            var named = first + i < a.args.Count ? a.args[first + i] : null;
                            if (call.Params[i].Name == "variableName" && named != null && named.source == KArgSource.Constant && named.stringValue == from)
                            { n++; if (write) named.stringValue = to; }
                        }
                    }
                }
            }
            foreach (var o in others ?? Enumerable.Empty<TripwireTrigger>())
            {
                if (o == null) continue;
                foreach (var e in o.events)
                    foreach (var a in FlatActions(e.actions))
                    {
                        // Set / Read Another Trigger's Variable: the parameter that names the other trigger's variable.
                        var spec = ActionCatalog.Get(a.actionId);
                        if (spec == null) continue;
                        for (int i = 0; i < a.args.Count && i < spec.Params.Length; i++)
                        {
                            if (!spec.Params[i].RemoteVariableRef || a.args[i]?.stringValue != from || RemoteTrigger(a) != t) continue;
                            n++;
                            if (!write) continue;
                            if (o != t) Undo.RecordObject(o, "Rename Tripwire Variable");
                            a.args[i].stringValue = to;
                            if (o != t) EditorUtility.SetDirty(o);
                        }
                    }
            }
            return n;
        }

        /// <summary>
        /// Whether a block of the trigger has an event value of that name ({player} in On Player Joined). A variable
        /// renamed to it would take over its {name} in texts, which name variables first.
        /// </summary>
        public static bool IsEventValueName(TripwireTrigger t, string name) =>
            t.events.Any(e => e != null && EventCatalog.Get(e.eventId) is EventSpec spec && spec.Params.Any(p => p.Name == name));

        /// <summary>A text with {from} placeholders (also { from } with spaces) written as {to}; {{ and }} stay.</summary>
        public static string RenameInTemplate(string text, string from, string to)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return text;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if ((c == '{' || c == '}') && i + 1 < text.Length && text[i + 1] == c) { sb.Append(c).Append(c); i++; continue; }
                int end = c == '{' ? text.IndexOf('}', i + 1) : -1;
                if (end < 0) { sb.Append(c); continue; }
                var name = text.Substring(i + 1, end - i - 1);
                sb.Append('{').Append(name == from || name.Trim() == from ? to : name).Append('}');
                i = end;
            }
            return sb.ToString();
        }

        /// <summary>
        /// The actions of a list in the generator's numbering (<see cref="ActionCall.Flatten"/>): each action, then an If
        /// block's then- and else-actions, depth first. Diagnostics and bindings refer to actions by this number.
        /// </summary>
        public static List<KAction> FlatActions(IEnumerable<KAction> actions) => ActionRules.Flatten(actions, a => a.actionId, a => a.thenActions, a => a.elseActions);

        static ActionCall ToCall(TripwireTrigger t, KAction a)
        {
            var call = new ActionCall { ActionId = a.actionId };
            var spec = ActionCatalog.Get(a.actionId);
            if (spec == null) return call;
            if (spec.HasConditions) // If, While
            {
                call.MatchAny = a.matchAny;
                foreach (var c in a.conditions) call.Conditions.Add(ToCondition(t, c));
                foreach (var x in a.thenActions) if (x != null) call.Then.Add(ToCall(t, x));
                foreach (var x in a.elseActions) if (x != null) call.Else.Add(ToCall(t, x));
                return call;
            }
            if (spec.Special == ActionSpecial.Call)
            {
                call.Call = a.actionId == ActionCatalog.ScriptCallId ? UdonSharpApi.Get(a.method) : UdonApi.Get(a.method);
                call.ResultVariable = string.IsNullOrEmpty(a.resultVariable) ? null : a.resultVariable;
                if (call.Call != null)
                {
                    var types = CallArgTypes(call.Call);
                    for (int i = 0; i < types.Count; i++)
                        call.Args.Add(i < a.args.Count ? TypedArg(types[i], a.args[i]) : null);
                }
                return call;
            }
            for (int i = 0; i < spec.Params.Length; i++)
            {
                var prm = spec.Params[i];
                var arg = i < a.args.Count ? a.args[i] : null;
                call.Args.Add(arg == null ? null : ToArg(t, a, prm, arg));
            }
            if (a.actionId == ActionCatalog.SetRemoteId || a.actionId == ActionCatalog.GetRemoteId)
            {
                call.RemoteType = RemoteType(a);
                var remoteName = a.args.Count > 1 ? a.args[1].stringValue : null;
                call.RemoteTemporary = RemoteTrigger(a)?.variables.FirstOrDefault(v => v.name == remoteName)?.temporary == true;
            }
            if (ActionCatalog.IsBlock(a.actionId)) // a loop: its body
                foreach (var x in a.thenActions) if (x != null) call.Then.Add(ToCall(t, x));
            return call;
        }

        /// <summary>
        /// Values that may pass through in-between values (amounts, positions, rotations). Whole numbers are usually
        /// states or counts: interpolated, 0 → 2 would pass through 1 on other screens.
        /// </summary>
        static bool SmoothedKind(ParamType type) =>
            type != null && !type.IsArray && (type.Kind == ValueKind.Float || type.Kind == ValueKind.Vector2 || type.Kind == ValueKind.Vector3 || type.Kind == ValueKind.Quaternion);

        /// <summary>The trigger a Set / Read Another Trigger's Variable action points at (its first argument).</summary>
        public static TripwireTrigger RemoteTrigger(KAction a)
        {
            var o = a.args.Count > 0 ? a.args[0].objects.FirstOrDefault(x => x != null) : null;
            // A trigger in the scene only: a prefab asset never runs, so reading it would get nothing.
            if (o == null || UnityEditor.EditorUtility.IsPersistent(o)) return null;
            return o as TripwireTrigger ?? (o as GameObject)?.GetComponent<TripwireTrigger>() ?? (o as Component)?.GetComponent<TripwireTrigger>();
        }

        /// <summary>The type of the other trigger's variable a remote action names, or null.</summary>
        public static ParamType RemoteType(KAction a)
        {
            var other = RemoteTrigger(a);
            return other == null || a.args.Count < 2 ? null : VariableType(other, a.args[1].stringValue);
        }

        // ---- batches: several triggers generated together (apply, the overview) share indexes of the scene ----

        static int batchDepth;
        static readonly Dictionary<(string what, UnityEngine.SceneManagement.Scene scene), object> batchCache = new Dictionary<(string, UnityEngine.SceneManagement.Scene), object>();

        /// <summary>While the scope lives, <see cref="PerScene"/> lookups are reused; the scene must not change meanwhile.</summary>
        public static IDisposable Batch() { batchDepth++; return new BatchScope(); }
        sealed class BatchScope : IDisposable
        {
            bool done;
            public void Dispose() { if (done) return; done = true; if (--batchDepth == 0) batchCache.Clear(); }
        }

        internal static T PerScene<T>(string what, UnityEngine.SceneManagement.Scene scene, Func<T> make) where T : class
        {
            if (batchDepth == 0) return make();
            if (batchCache.TryGetValue((what, scene), out var cached)) return (T)cached;
            var value = make();
            batchCache[(what, scene)] = value;
            return value;
        }

        /// <summary>Whether a trigger in the same scene sets this variable of t ("Set Another Trigger's Variable").</summary>
        static bool SetFromOutside(TripwireTrigger t, string variable)
        {
            var scene = t.gameObject.scene;
            if (!scene.IsValid()) return false;
            var set = PerScene("set from outside", scene, () =>
            {
                var found = new HashSet<(TripwireTrigger, string)>();
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var other in root.GetComponentsInChildren<TripwireTrigger>(true))
                        foreach (var e in other.events)
                            foreach (var a in FlatActions(e.actions))
                                if (a.actionId == ActionCatalog.SetRemoteId && a.args.Count > 1 && RemoteTrigger(a) is TripwireTrigger target)
                                    found.Add((target, a.args[1].stringValue));
                return found;
            });
            return set.Contains((t, variable));
        }

        /// <summary>The value type of a parameter whose type depends on the chosen variable (Set/Add Variable).</summary>
        public static ParamType EffectiveType(TripwireTrigger t, KAction a, ActionParam prm)
        {
            if (prm.Type != null) return prm.Type;
            if (a.actionId == ActionCatalog.SetRemoteId) return RemoteType(a); // the value takes the other trigger's variable's type
            var varName = a.args.Count > 0 ? a.args[0].stringValue : null;
            var type = VariableType(t, varName);
            if (a.actionId == ActionCatalog.CalculateId && prm.Name == "b" && a.args.Count > 2)
                return CodeGenerator.CalculateOperandB(type, (ActionCatalog.CalcOp)a.args[2].intValue);
            return type;
        }

        /// <summary>Argument types of a call in order: [instance,] then parameters.</summary>
        public static List<ParamType> CallArgTypes(CallSpec c)
        {
            var list = new List<ParamType>();
            if (c.Instance != null) list.Add(c.Instance);
            foreach (var p in c.Params) list.Add(p.Type);
            return list;
        }

        static ArgValue ToArg(TripwireTrigger t, KAction a, ActionParam prm, KArg arg)
        {
            if (prm.Choices != null) return ArgValue.Const(arg.intValue);
            if (prm.VariableRef || prm.TimerRef || prm.RemoteVariableRef) return ArgValue.Const(arg.stringValue ?? "");
            var type = EffectiveType(t, a, prm);
            if (type == null) return ArgValue.Const(null);
            return TypedArg(type, arg);
        }

        static ArgValue TypedArg(ParamType type, KArg arg)
        {
            switch (arg.source)
            {
                // A single-object parameter uses one slot, the first, as the Inspector shows it: an empty list is one
                // empty slot, and entries left from an array this argument once was are not used.
                case KArgSource.Objects: return ArgValue.Objs(type != null && !type.IsArray ? 1 : arg.objects.Count);
                case KArgSource.Self: return ArgValue.SelfObject();
                case KArgSource.LocalPlayer: return ArgValue.Local();
                case KArgSource.EventParam: return ArgValue.Param(arg.name);
                default: return ValueArg(arg, type);
            }
        }

        static ArgValue ValueArg(KArg arg, ParamType type)
        {
            if (arg.source == KArgSource.Variable) return ArgValue.Var(arg.name);
            return ArgValue.Const(ConstOf(type, arg));
        }

        /// <summary>Constant value of an argument for a type (null for kinds without literals, or an unset enum).</summary>
        public static object ConstOf(ParamType type, KArg arg)
        {
            if (type.Kind == ValueKind.Enum) return string.IsNullOrEmpty(arg?.stringValue) ? null : arg.stringValue;
            return ConstOf(type.Kind, arg);
        }

        public static object ConstOf(ValueKind kind, KArg arg)
        {
            if (arg == null) return null;
            switch (kind)
            {
                case ValueKind.Bool: return arg.boolValue;
                case ValueKind.Int: return arg.intValue;
                case ValueKind.Float: return arg.floatValue;
                case ValueKind.String: return arg.stringValue ?? "";
                case ValueKind.Vector3: return new[] { arg.vectorValue.x, arg.vectorValue.y, arg.vectorValue.z };
                case ValueKind.Vector2: return new[] { arg.vector4Value.x, arg.vector4Value.y };
                case ValueKind.Color: return new[] { arg.vector4Value.x, arg.vector4Value.y, arg.vector4Value.z, arg.vector4Value.w };
                case ValueKind.Quaternion: return new[] { arg.vector4Value.x, arg.vector4Value.y, arg.vector4Value.z };
                case ValueKind.Enum: return arg.stringValue ?? "";
                case ValueKind.Url: return arg.stringValue ?? "";
                default: return null;
            }
        }

        // ---- type resolution / reference coercion ----

        // Misses are cached too: new types only come with a domain reload, which empties this.
        static readonly Dictionary<string, Type> typeCache = new Dictionary<string, Type>();

        public static Type ResolveType(string fullName)
        {
            if (fullName == null) return null;
            Type t;
            if (typeCache.TryGetValue(fullName, out t)) return t;
            // C# names nested types with '.', reflection with '+': try "A.B.C", then "A.B+C", "A+B+C".
            var candidate = fullName;
            for (int attempt = 0; t == null && attempt < 4; attempt++)
            {
                var name = candidate;
                t = AppDomain.CurrentDomain.GetAssemblies().Select(asm => asm.GetType(name, false)).FirstOrDefault(x => x != null);
                int dot = candidate.LastIndexOf('.');
                if (dot < 0) break;
                candidate = candidate.Substring(0, dot) + "+" + candidate.Substring(dot + 1);
            }
            typeCache[fullName] = t;
            return t;
        }

        /// <summary>
        /// Turn whatever the user dragged in (GameObject, any component, a TripwireTrigger) into what the parameter needs.
        /// Returns null when it cannot be converted.
        /// </summary>
        public static Object Coerce(Object o, Type want)
        {
            if (o == null || want == null) return null;
            if (want == typeof(UdonBehaviour))
            {
                var kt = o as TripwireTrigger ?? (o as GameObject)?.GetComponent<TripwireTrigger>() ?? (o as Component)?.GetComponent<TripwireTrigger>();
                if (kt != null && kt.generated != null) return kt.generated;
            }
            if (want.IsInstanceOfType(o)) return o;
            var go = o as GameObject ?? (o as Component)?.gameObject;
            if (go == null) return null;
            if (want == typeof(GameObject)) return go;
            if (typeof(Component).IsAssignableFrom(want)) return go.GetComponent(want);
            return null;
        }
    }
}
