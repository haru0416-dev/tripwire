using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Tripwire.Core;
using UdonSharp;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tripwire.Editor
{
    /// <summary>
    /// What a trigger can use on another UdonSharp script: its public methods, fields and properties, read by
    /// reflection from the script's class. Generated code calls them on the class directly (tw_T.Play(),
    /// tw_T.volume = 0.5f), and UdonSharp turns that into its usual cross-behaviour calls. Works for any asset written
    /// in U# (video players, pens, gimmicks) without per-asset code.
    /// </summary>
    public static class UdonSharpApi
    {
        /// <summary>Member ids, "usharp:Type::m:Method(T1,T2)" and the like. Saved in scenes: the format can't change.</summary>
        internal const string Prefix = "usharp:";

        internal static Type TypeOfId(string id)
        {
            if (string.IsNullOrEmpty(id) || !id.StartsWith(Prefix, StringComparison.Ordinal)) return null;
            int sep = id.IndexOf("::", StringComparison.Ordinal);
            return sep < 0 ? null : TripwireModel.ResolveType(id.Substring(Prefix.Length, sep - Prefix.Length));
        }
        static readonly Dictionary<Type, List<CallSpec>> cache = new Dictionary<Type, List<CallSpec>>();

        /// <summary>The user U# scripts on a GameObject (or on a component's GameObject), Tripwire's own excluded.</summary>
        public static List<Type> ScriptsOn(Object o)
        {
            var go = o as GameObject ?? (o as Component)?.gameObject;
            if (go == null) return new List<Type>();
            return go.GetComponents<UdonSharpBehaviour>()
                .Where(b => b != null && b.GetType().Namespace != CodeGenerator.Namespace)
                .Select(b => b.GetType())
                .Distinct()
                .ToList();
        }

        static HashSet<string> reachableAssemblies;

        /// <summary>
        /// Generated scripts compile into Assembly-CSharp, which only sees auto-referenced assemblies. A script inside an
        /// assembly definition with autoReferenced off cannot be named there, and using it would break the project's compile.
        /// </summary>
        public static bool IsReachable(Type t)
        {
            if (reachableAssemblies == null)
            {
                reachableAssemblies = new HashSet<string>(StringComparer.Ordinal) { "Assembly-CSharp" };
                var csharp = UnityEditor.Compilation.CompilationPipeline.GetAssemblies(UnityEditor.Compilation.AssembliesType.PlayerWithoutTestAssemblies)
                    .FirstOrDefault(a => a.name == "Assembly-CSharp");
                if (csharp != null)
                    foreach (var r in csharp.assemblyReferences) reachableAssemblies.Add(r.name);
            }
            return reachableAssemblies.Contains(t.Assembly.GetName().Name);
        }

        public static CallSpec Get(string id)
        {
            var type = TypeOfId(id);
            return type == null ? null : Members(type).FirstOrDefault(c => c.UdonName == id);
        }

        public static IReadOnlyList<CallSpec> Members(Type t)
        {
            if (t == null) return new List<CallSpec>();
            List<CallSpec> list;
            if (cache.TryGetValue(t, out list)) return list;
            list = new List<CallSpec>();
            cache[t] = list;
            if (!typeof(UdonSharpBehaviour).IsAssignableFrom(t)) return list;

            var instance = ParamType.Objects(UdonApi.CSharpName(t));
            instance.IsComponent = true;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;

            foreach (var m in t.GetMethods(flags).Where(m => Declared(m.DeclaringType) && !m.IsSpecialName && !m.IsGenericMethod).OrderBy(m => m.Name))
            {
                var ps = m.GetParameters();
                if (ps.Any(p => p.ParameterType.IsByRef || p.IsOut)) continue;
                var types = ps.Select(p => ParamFor(p.ParameterType)).ToList();
                if (types.Any(x => x == null)) continue;
                var c = new CallSpec
                {
                    UdonName = Id(t, "m", m.Name, ps.Select(p => p.ParameterType)),
                    DeclaringType = UdonApi.CSharpName(t),
                    Member = m.Name,
                    Kind = CallKind.Method,
                    Instance = instance,
                    Returns = m.ReturnType == typeof(void) ? null : ParamFor(m.ReturnType, false),
                };
                for (int i = 0; i < ps.Length; i++) c.Params.Add(new EventParam(ps[i].Name, types[i]));
                list.Add(c);
            }

            foreach (var f in t.GetFields(flags).Where(f => Declared(f.DeclaringType)).OrderBy(f => f.Name))
                AddValue(list, t, instance, f.Name, ParamFor(f.FieldType, false), ParamFor(f.FieldType), true, !f.IsInitOnly);

            foreach (var p in t.GetProperties(flags).Where(p => Declared(p.DeclaringType) && p.GetIndexParameters().Length == 0).OrderBy(p => p.Name))
                AddValue(list, t, instance, p.Name, ParamFor(p.PropertyType, false), ParamFor(p.PropertyType), p.GetGetMethod() != null, p.GetSetMethod() != null);

            return list;
        }

        /// <summary>Members declared by the user's scripts, not by UdonSharpBehaviour / MonoBehaviour.</summary>
        static bool Declared(Type declaring) =>
            declaring != null && typeof(UdonSharpBehaviour).IsAssignableFrom(declaring) && declaring != typeof(UdonSharpBehaviour);

        static void AddValue(List<CallSpec> list, Type t, ParamType instance, string name, ParamType readType, ParamType type, bool canRead, bool canWrite)
        {
            if (type == null) return;
            if (canRead)
                list.Add(new CallSpec { UdonName = Id(t, "get", name, null), DeclaringType = UdonApi.CSharpName(t), Member = name, Kind = CallKind.Get, Instance = instance, Returns = readType });
            if (canWrite)
            {
                var set = new CallSpec { UdonName = Id(t, "set", name, null), DeclaringType = UdonApi.CSharpName(t), Member = name, Kind = CallKind.Set, Instance = instance };
                set.Params.Add(new EventParam("value", type));
                list.Add(set);
            }
        }

        /// <param name="asParameter">A value passed in (small integers take int values); false for values read out.</param>
        static ParamType ParamFor(Type pt, bool asParameter = true)
        {
            // Small integers (VizVid's byte playerType): entered as int, cast in the call.
            if (asParameter && (pt == typeof(byte) || pt == typeof(sbyte) || pt == typeof(short) || pt == typeof(ushort)))
                return new ParamType(ValueKind.Int, pt.FullName);
            if (typeof(UdonSharpBehaviour).IsAssignableFrom(pt) && !pt.IsArray)
                return new ParamType(ValueKind.Object, UdonApi.CSharpName(pt)) { IsComponent = true };
            return UdonApi.ValueTypeOf(pt);
        }

        /// <summary>A method a trigger can call at Start to subscribe: one parameter takes the trigger itself.</summary>
        public sealed class Registration
        {
            public string Id;
            public string Method;
            public string DeclaringType;
            public List<EventParam> Params = new List<EventParam>();
            public int SelfIndex;
            /// <summary>Leading parameters to pass; the rest have defaults or are `params`.</summary>
            public int PassCount;
            /// <summary>All parameters of the method, passed or not (tells overloads apart).</summary>
            public int TotalParams;
        }

        static readonly Dictionary<Type, List<Registration>> registrationCache = new Dictionary<Type, List<Registration>>();

        /// <summary>
        /// Public methods of a U# script that take any UdonSharpBehaviour in one parameter: the shape of listener
        /// registration (ProTV _RegisterListener(listener, priority = 0), USharpVideo RegisterCallbackReceiver(behaviour),
        /// VideoTXL _Register(index, handler, eventName, params args)...). Typed listener bases (YamaPlayerListener) don't match.
        /// </summary>
        public static IReadOnlyList<Registration> Registrations(Type t)
        {
            List<Registration> list;
            if (t == null) return new List<Registration>();
            if (registrationCache.TryGetValue(t, out list)) return list;
            list = new List<Registration>();
            registrationCache[t] = list;
            if (!typeof(UdonSharpBehaviour).IsAssignableFrom(t)) return list;

            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                         .Where(m => Declared(m.DeclaringType) && !m.IsSpecialName && !m.IsGenericMethod && m.ReturnType == typeof(void)).OrderBy(m => m.Name))
            {
                var ps = m.GetParameters();
                int pass = 0;
                while (pass < ps.Length && !ps[pass].IsOptional && !ps[pass].IsDefined(typeof(ParamArrayAttribute), false)) pass++;
                var selves = Enumerable.Range(0, pass).Where(i => !ps[i].ParameterType.IsByRef && ps[i].ParameterType.IsAssignableFrom(typeof(UdonSharpBehaviour))).ToList();
                if (selves.Count != 1) continue;
                var r = new Registration
                {
                    Id = Id(t, "m", m.Name, ps.Select(p => p.ParameterType)),
                    Method = m.Name, DeclaringType = UdonApi.CSharpName(t), SelfIndex = selves[0], PassCount = pass, TotalParams = ps.Length,
                };
                bool ok = true;
                for (int i = 0; i < pass && ok; i++)
                {
                    var type = i == r.SelfIndex ? ParamType.Objects(UdonApi.CSharpName(ps[i].ParameterType)) : ParamFor(ps[i].ParameterType);
                    if (type == null || ps[i].ParameterType.IsByRef) ok = false;
                    else if (i != r.SelfIndex && (type.Kind == ValueKind.Object || type.Kind == ValueKind.Player)) ok = false; // values only
                    else r.Params.Add(new EventParam(ps[i].Name, type));
                }
                if (ok) list.Add(r);
            }
            return list;
        }

        public static Registration GetRegistration(Type t, string id) => Registrations(t).FirstOrDefault(r => r.Id == id);

        static string Id(Type t, string kind, string name, IEnumerable<Type> ps) =>
            Prefix + t.FullName + "::" + kind + ":" + name + (ps == null ? "" : "(" + string.Join(",", ps.Select(p => p.FullName)) + ")");
    }
}
