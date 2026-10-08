using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Tripwire.Core;

namespace Tripwire.Editor
{
    /// <summary>
    /// Applies <see cref="AssetPresets"/> to the scripts actually in the project: finds the preset for a script type,
    /// and resolves its registrations and operations against the installed version (anything the version lacks is left out).
    /// </summary>
    internal static class PresetResolver
    {
        public static AssetPresets.Preset For(Type script)
        {
            if (script == null) return null;
            foreach (var p in AssetPresets.All)
            {
                var type = TripwireModel.ResolveType(p.ScriptType);
                if (type != null && type.IsAssignableFrom(script)) return p;
            }
            return null;
        }

        public static UdonSharpApi.Registration RegistrationFor(Type script, AssetPresets.Notification n) =>
            UdonSharpApi.Registrations(script).FirstOrDefault(r => r.Method == n.RegisterMethod && r.TotalParams == n.RegisterParamCount);

        /// <summary>Notifications whose registration method exists on this script.</summary>
        public static List<AssetPresets.Notification> Notifications(Type script)
        {
            var p = For(script);
            return p == null ? new List<AssetPresets.Notification>() : p.Notifications.Where(n => RegistrationFor(script, n) != null && SentBy(script, n)).ToList();
        }

        static bool SentBy(Type script, AssetPresets.Notification n)
        {
            if (n.OnlyFor == null) return true;
            var only = TripwireModel.ResolveType(n.OnlyFor);
            return only != null && only.IsAssignableFrom(script);
        }

        /// <summary>Fills a listen event with a preset notification.</summary>
        public static bool Apply(KEvent e, Type script, AssetPresets.Notification n)
        {
            var r = RegistrationFor(script, n);
            if (r == null) return false;
            e.name = n.Callback;
            e.listenMethod = r.Id;
            e.listenPrepare = n.Prepare ?? "";
            e.listenArgs.Clear();
            for (int k = 0; k < r.Params.Count; k++)
            {
                var arg = new KArg();
                if (n.RegisterArgs.TryGetValue(k, out var v)) SetConstant(arg, v is string s && s == AssetPresets.CallbackPlaceholder ? n.Callback : v);
                e.listenArgs.Add(arg);
            }
            return true;
        }

        /// <summary>The preset notification this event is set up as, or null (also when edited by hand afterwards).</summary>
        public static AssetPresets.Notification Matching(KEvent e, Type script)
        {
            foreach (var n in Notifications(script))
            {
                var r = RegistrationFor(script, n);
                if (e.name != n.Callback || e.listenMethod != r.Id || (e.listenPrepare ?? "") != (n.Prepare ?? "")) continue;
                bool same = true;
                foreach (var kv in n.RegisterArgs)
                {
                    var want = kv.Value is string s && s == AssetPresets.CallbackPlaceholder ? n.Callback : kv.Value;
                    var arg = kv.Key < e.listenArgs.Count ? e.listenArgs[kv.Key] : null;
                    if (arg == null || arg.source != KArgSource.Constant || !SameConstant(arg, want)) { same = false; break; }
                }
                if (same) return n;
            }
            return null;
        }

        /// <summary>The members an operation calls on this script, or null when this version lacks one.</summary>
        public static List<CallSpec> Resolve(Type script, AssetPresets.Operation op)
        {
            var calls = new List<CallSpec>();
            foreach (var step in op.Steps)
            {
                var id = step.ParamTypes == null
                    ? UdonSharpApi.Prefix + script.FullName + "::" + step.Member
                    : UdonSharpApi.Prefix + script.FullName + "::m:" + step.Member + "(" + string.Join(",", step.ParamTypes) + ")";
                var c = UdonSharpApi.Members(script).FirstOrDefault(m => m.UdonName == id);
                if (c == null) return null;
                calls.Add(c);
            }
            return calls;
        }

        public static List<AssetPresets.Operation> Operations(Type script)
        {
            var p = For(script);
            return p == null ? new List<AssetPresets.Operation>() : p.Operations.Where(o => Resolve(script, o) != null).ToList();
        }

        /// <summary>The "use another script" actions for an operation, targeting the given objects.</summary>
        public static List<KAction> Actions(Type script, AssetPresets.Operation op, List<UnityEngine.Object> targets)
        {
            var calls = Resolve(script, op);
            var actions = new List<KAction>();
            for (int i = 0; i < calls.Count; i++)
            {
                var a = new KAction { actionId = ActionCatalog.ScriptCallId, method = calls[i].UdonName };
                a.args.Add(new KArg { source = KArgSource.Objects, objects = new List<UnityEngine.Object>(targets) });
                for (int k = 0; k < calls[i].Params.Count; k++)
                {
                    var arg = new KArg();
                    if (op.Steps[i].Defaults.TryGetValue(k, out var v)) SetConstant(arg, v);
                    else if (calls[i].Params[k].Type.Kind == ValueKind.Bool) arg.boolValue = true;
                    a.args.Add(arg);
                }
                actions.Add(a);
            }
            return actions;
        }

        static void SetConstant(KArg arg, object v)
        {
            arg.source = KArgSource.Constant;
            switch (v)
            {
                case bool b: arg.boolValue = b; break;
                case int n: arg.intValue = n; break;
                case float f: arg.floatValue = f; break;
                case string s: arg.stringValue = s; break;
            }
        }

        static bool SameConstant(KArg arg, object v)
        {
            switch (v)
            {
                case bool b: return arg.boolValue == b;
                case int n: return arg.intValue == n;
                case float f: return arg.floatValue.Equals(f);
                case string s: return arg.stringValue == s;
                default: return Convert.ToString(v, CultureInfo.InvariantCulture) == arg.stringValue;
            }
        }
    }
}
