using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Tripwire.Core;
using VRC.SDKBase;
using VRC.Udon.Editor;
using VRC.Udon.Graph;
using UdonBridge;

namespace Tripwire.Editor
{
    /// <summary>
    /// The Udon API members a trigger can call, read from Udon's own node definitions (the authority on what is exposed).
    /// Members whose parameter types the Inspector cannot enter yet are left out.
    /// </summary>
    public static class UdonApi
    {
        static List<CallSpec> all;
        static Dictionary<string, CallSpec> byName;
        /// <summary>Types Udon can hold in a variable (Variable_* nodes). Generated fields must use these.</summary>
        static HashSet<Type> variableTypes;
        /// <summary>Types whose == operator Udon exposes (op_Equality nodes).</summary>
        static HashSet<Type> equalityTypes;

        public static IReadOnlyList<CallSpec> All
        {
            get { Build(); return all; }
        }

        public static CallSpec Get(string udonName)
        {
            if (string.IsNullOrEmpty(udonName)) return null;
            Build();
            CallSpec c;
            return byName.TryGetValue(udonName, out c) ? c : null;
        }

        static bool installed;

        /// <summary>
        /// Give the generator what only the editor knows: real type checks (subclasses, list item types, constant
        /// construction) and the method names U# treats as Udon events (Event_* nodes), which Custom events avoid.
        /// Call before generating (TripwireCompiler.Generate does).
        /// </summary>
        public static void InstallEditorKnowledge()
        {
            if (installed) return;
            installed = true;
            InstallTypeCheck();
            CodeGenerator.EnumMembersOf = name => TripwireModel.ResolveType(name) is Type t && t.IsEnum ? EnumMembers(t) : null;
            // The list UdonSharp checks synced fields against (an unknown type is left to the other checks).
            CodeGenerator.CanSync = type => TripwireModel.ResolveType(CodeGenerator.FullTypeName(type)) is not Type t || VRC.Udon.UdonNetworkTypes.CanSync(t);
            foreach (var d in UdonNodes.Events)
            {
                var name = d.fullName.Substring("Event_".Length);
                if (name.Length > 0 && name != EventCatalog.CustomId) CodeGenerator.UdonEventNames.Add(name);
            }
        }

        /// <summary>Types the Udon assembler can resolve (Type_ nodes): constants of these can appear in a program.</summary>
        static HashSet<Type> resolvable;

        static void Build()
        {
            if (all != null) return;
            all = new List<CallSpec>();
            byName = new Dictionary<string, CallSpec>(StringComparer.Ordinal);
            var defs = UdonNodes.All;
            resolvable = new HashSet<Type>(UdonNodes.ResolvableTypes);
            variableTypes = new HashSet<Type>(UdonNodes.VariableTypes);
            equalityTypes = new HashSet<Type>(UdonNodes.EqualityTypes);
            foreach (var d in defs)
            {
                var c = TryConvert(d);
                if (c == null || byName.ContainsKey(c.UdonName)) continue;
                all.Add(c);
                byName.Add(c.UdonName, c);
            }
            all.Sort((a, b) => string.CompareOrdinal(a.DeclaringType + "." + a.Member, b.DeclaringType + "." + b.Member));
        }

        /// <summary>Udon's type id: the full name without '.' and '+', e.g. UnityEngineTransform.</summary>
        static string UdonTypeId(Type t) => t.FullName?.Replace(".", "").Replace("+", "");

        public static string CSharpName(Type t) => t.FullName.Replace('+', '.');

        static CallSpec TryConvert(UdonNodeDefinition d)
        {
            var full = d.fullName;
            int dot = full.IndexOf(".__", StringComparison.Ordinal);
            if (dot < 0 || d.type == null) return null;
            var declaring = d.type;
            // Node "type" is the category; skip the few nodes filed under another type (e.g. VRCInstantiate).
            if (UdonTypeId(declaring) != full.Substring(0, dot)) return null;
            if (declaring.IsGenericType || declaring.IsArray || declaring.ContainsGenericParameters) return null;

            var rest = full.Substring(dot + 3);
            int sep = rest.IndexOf("__", StringComparison.Ordinal);
            var name = sep < 0 ? rest : rest.Substring(0, sep);
            if (name.StartsWith("op_", StringComparison.Ordinal) || name == "get_Item" || name == "set_Item") return null;
            // Generic methods (T / TArray placeholders in the signature).
            foreach (var token in rest.Split('_'))
                if (token == "T" || token == "TArray") return null;

            var c = new CallSpec { UdonName = full, DeclaringType = CSharpName(declaring), Member = name, Kind = name == "ctor" ? CallKind.Ctor : CallKind.Method };
            var ps = d.parameters;
            int start = 0;
            bool valueTypeInstance = false;
            if (c.Kind != CallKind.Ctor && ps.Count > 0 && ps[0].name == "instance" && ps[0].parameterType == UdonNodeParameter.ParameterType.IN)
            {
                c.Instance = InstanceType(ps[0].type);
                if (c.Instance == null) return null;
                valueTypeInstance = ps[0].type.IsValueType;
                start = 1;
            }

            bool returnsSomething = false;
            var inTypes = new List<Type>();
            var inOut = new List<int>(); // parameters Udon marks IN_OUT (indices into c.Params)
            for (int i = start; i < ps.Count; i++)
            {
                var p = ps[i];
                switch (p.parameterType)
                {
                    case UdonNodeParameter.ParameterType.IN:
                        var t = ArgumentType(p.type);
                        if (t == null) return null;
                        c.Params.Add(new EventParam(string.IsNullOrEmpty(p.name) ? "arg" + (i - start) : p.name, t));
                        inTypes.Add(p.type);
                        break;
                    case UdonNodeParameter.ParameterType.OUT when string.IsNullOrEmpty(p.name) || p.name == "clone":
                        // The return value (Udon names Instantiate's "clone").
                        returnsSomething = true;
                        c.Returns = ValueType(p.type);
                        // An enum Udon has no variable type for, but can resolve: kept as its number.
                        if (c.Returns == null && ArgumentType(p.type) is ParamType asEnum && asEnum.Kind == ValueKind.Enum)
                        {
                            c.Returns = ParamType.Of(ValueKind.Int);
                            c.ReturnsEnum = CSharpName(p.type);
                        }
                        break;
                    case UdonNodeParameter.ParameterType.IN_OUT:
                        // An `out` / `ref` parameter (Udon has one kind for both; the C# method tells which), or an array the
                        // method fills in place. Either way a variable of the value's type takes part.
                        var element = p.type.IsByRef ? p.type.GetElementType() : p.type;
                        var vt = ValueType(element);
                        if (vt == null) return null;
                        c.Params.Add(new EventParam(string.IsNullOrEmpty(p.name) ? "arg" + (i - start) : p.name, vt));
                        inTypes.Add(element);
                        inOut.Add(c.Params.Count - 1);
                        break;
                    default:
                        return null;
                }
            }
            if (inOut.Count > 0)
            {
                // Which are `out`, which `ref`, which plain arrays: read from the C# method itself.
                if (c.Kind != CallKind.Method || !ResolvePasses(declaring, name, c, inTypes, inOut)) return null;
                // In Udon an extern's `ref` argument never comes back (Mathf.SmoothDamp's velocity stays 0; checked in
                // ClientSim with SDK 3.10.5), while `out` does: offering `ref` would silently do nothing.
                if (c.Params.Any(x => x.Pass == ParamPass.Ref)) return null;
                for (int i = 0; i < c.Params.Count; i++)
                    if (c.Params[i].Receives) inTypes[i] = inTypes[i].MakeByRefType();
            }

            if (c.Kind == CallKind.Ctor)
            {
                if (c.Returns == null) return null; // a value we cannot store
            }
            else if (name.StartsWith("get_", StringComparison.Ordinal) && c.Params.Count == 0 && returnsSomething)
            {
                c.Kind = CallKind.Get;
                c.Member = name.Substring(4);
                if (c.Returns == null) return null; // nothing to store it in
            }
            else if (name.StartsWith("set_", StringComparison.Ordinal) && c.Params.Count == 1 && !returnsSomething)
            {
                c.Kind = CallKind.Set;
                c.Member = name.Substring(4);
            }
            // Accessors that are not plain properties (indexers such as StringBuilder.get_Chars(int), event add_/remove_)
            // cannot be called by name from C#.
            if (c.Kind == CallKind.Method && (name.StartsWith("get_", StringComparison.Ordinal) || name.StartsWith("set_", StringComparison.Ordinal)
                                             || name.StartsWith("add_", StringComparison.Ordinal) || name.StartsWith("remove_", StringComparison.Ordinal)))
                return null;
            // On a struct, setters and void methods (Vector3.y, Vector3.Normalize, VRCTweenHandle.Kill) act on a copy under
            // UdonSharp's value semantics: the generator makes them work on a variable and stores the copy back.
            c.InstanceIsStruct = valueTypeInstance;
            // Generated code is compiled by Unity as plain C# first, so the member must exist in the installed
            // assemblies (Udon's list can be ahead of a package, e.g. Cinemachine) and must not be [Obsolete(error)].
            if (!IsCallableFromCSharp(declaring, c, inTypes.ToArray())) return null;
            return c;
        }

        /// <summary>
        /// Sets each parameter Udon marks IN_OUT (<paramref name="inOut"/>) to Out, Ref or Fill (an array filled in place)
        /// from the public C# method with this name and these parameter types (by-ref or not). False when there is no single one.
        /// </summary>
        static bool ResolvePasses(Type declaring, string member, CallSpec c, List<Type> types, List<int> inOut)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;
            var found = declaring.GetMethods(flags).Where(m => m.Name == member).Where(m =>
            {
                var ps = m.GetParameters();
                if (ps.Length != types.Count) return false;
                for (int i = 0; i < ps.Length; i++)
                {
                    var t = ps[i].ParameterType;
                    if ((t.IsByRef ? t.GetElementType() : t) != types[i]) return false;
                    bool marked = inOut.Contains(i);
                    if (t.IsByRef ? !marked : marked && !t.IsArray) return false;
                }
                return true;
            }).ToList();
            if (found.Count != 1) return false;
            var parameters = found[0].GetParameters();
            foreach (int i in inOut)
            {
                var t = parameters[i].ParameterType;
                c.Params[i].Pass = !t.IsByRef ? ParamPass.Fill : parameters[i].IsOut ? ParamPass.Out : ParamPass.Ref;
            }
            return true;
        }

        static bool IsCallableFromCSharp(Type declaring, CallSpec c, Type[] paramTypes)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;
            MemberInfo m;
            try
            {
                if (c.Kind == CallKind.Ctor) m = declaring.GetConstructor(paramTypes);
                else if (c.Kind == CallKind.Method) m = declaring.GetMethod(c.Member, flags, null, paramTypes, null);
                else m = (MemberInfo)declaring.GetProperty(c.Member, flags) ?? declaring.GetField(c.Member, flags);
            }
            catch (AmbiguousMatchException)
            {
                return true; // exists (several overloads); exact overload checked by the compile test
            }
            return m != null && !IsObsoleteError(m);
        }

        static bool IsObsoleteError(MemberInfo m) =>
            m.GetCustomAttributes(typeof(ObsoleteAttribute), true).Cast<ObsoleteAttribute>().Any(o => o.IsError);

        /// <summary>Enum member names usable in code: obsolete members are left out (some are compile errors).</summary>
        public static string[] EnumMembers(Type enumType)
        {
            if (enumType == null || !enumType.IsEnum) return new string[0];
            return enumType.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.GetCustomAttributes(typeof(ObsoleteAttribute), false).Length == 0)
                .Select(f => f.Name)
                .ToArray();
        }

        /// <summary>Kinds the Inspector can enter as a constant (everything else comes from variables or objects).</summary>
        public static bool HasEditableConstant(ParamType t) =>
            !t.IsArray && t.Kind != ValueKind.Object && t.Kind != ValueKind.Player && t.Kind != ValueKind.Other;

        /// <summary>Every type a trigger variable can have: the types Udon can hold in a variable, minus generics.</summary>
        public static IReadOnlyList<Type> VariableTypes
        {
            get
            {
                Build();
                return variableTypes.Where(t => VariableParamType(t) != null).OrderBy(t => CSharpName(t), StringComparer.Ordinal).ToList();
            }
        }

        /// <summary>Generator type for a variable of type <paramref name="t"/> (arrays of objects/players become target lists).</summary>
        public static ParamType VariableParamType(Type t)
        {
            Build();
            if (t == null || !variableTypes.Contains(t) || t.IsGenericType || t.ContainsGenericParameters) return null;
            if (t.IsArray && t.GetArrayRank() == 1)
            {
                var e = t.GetElementType();
                if (e == typeof(VRCPlayerApi)) return new ParamType(ValueKind.Player, null, true);
                if (typeof(UnityEngine.Object).IsAssignableFrom(e) && !e.IsGenericType)
                    return new ParamType(ValueKind.Object, CSharpName(e), true) { IsComponent = typeof(UnityEngine.Component).IsAssignableFrom(e) };
            }
            return ValueType(t);
        }

        /// <summary>Install the editor's knowledge into the generator: real type assignability (subclasses, boxing).</summary>
        static void InstallTypeCheck()
        {
            CodeGenerator.CheckConstantConstruction = CheckConstantConstruction;
            var basicElement = CodeGenerator.ElementTypeOf;
            CodeGenerator.ElementTypeOf = t =>
            {
                if (t == null || t.IsArray || t.Kind != ValueKind.Other) return basicElement(t);
                var type = TripwireModel.ResolveType(t.UnityType);
                return type != null && type.IsArray && type.GetArrayRank() == 1 ? ValueType(type.GetElementType()) : null;
            };
            CodeGenerator.TypeAssignable = (want, have) =>
            {
                var tw = TripwireModel.ResolveType(want);
                var th = TripwireModel.ResolveType(have);
                return tw != null && th != null && tw.IsAssignableFrom(th);
            };
        }

        /// <summary>Run the constructor U# would fold, the way U# runs it (worker thread), and report a failure.</summary>
        static string CheckConstantConstruction(CallSpec c, List<ArgValue> args)
        {
            var type = TripwireModel.ResolveType(c.DeclaringType);
            if (type == null || !type.IsValueType) return null; // only value-type constructors are folded
            var values = new object[args.Count];
            for (int i = 0; i < args.Count; i++)
            {
                var pt = TripwireModel.ResolveType(CodeGenerator.FullTypeName(c.Params[i].Type));
                if (pt == null) return null;
                values[i] = ToClr(pt, args[i].Constant);
            }
            Exception failure = null;
            var worker = new System.Threading.Thread(() =>
            {
                try { Activator.CreateInstance(type, values); }
                catch (Exception e) { failure = e is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : e; }
            });
            worker.Start();
            if (!worker.Join(5000)) return null;
            return failure == null ? null
                : "UdonSharp evaluates new " + type.Name + "(...) with these constant values while compiling, and it fails: " + failure.Message
                  + " Use different values, or pass one of them from a variable.";
        }

        static object ToClr(Type t, object constant)
        {
            if (t.IsEnum) return Enum.Parse(t, (string)constant);
            if (constant is float[] f)
            {
                if (t == typeof(UnityEngine.Vector2)) return new UnityEngine.Vector2(f[0], f[1]);
                if (t == typeof(UnityEngine.Vector3)) return new UnityEngine.Vector3(f[0], f[1], f[2]);
                if (t == typeof(UnityEngine.Color)) return new UnityEngine.Color(f[0], f[1], f[2], f[3]);
                if (t == typeof(UnityEngine.Quaternion)) { var q = UnityMath.Euler(f[0], f[1], f[2]); return new UnityEngine.Quaternion(q[0], q[1], q[2], q[3]); }
            }
            return Convert.ChangeType(constant, t, System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Whether generated code may write `a == b` for this type: a user-defined == must be exposed to Udon
        /// (e.g. AnimationState defines one that is not); without one, reference types compare by reference.
        /// </summary>
        static bool HasEqualityOperator(Type t)
        {
            if (t.IsPrimitive || t.IsEnum) return true;
            for (var x = t; x != null; x = x.BaseType)
            {
                bool declares = x.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Any(m => m.Name == "op_Equality");
                if (declares) return equalityTypes.Contains(x);
            }
            return !t.IsValueType;
        }

        static ParamType InstanceType(Type t)
        {
            if (t == typeof(VRCPlayerApi)) return ParamType.Of(ValueKind.Player);
            if (typeof(UnityEngine.Object).IsAssignableFrom(t) && !t.IsArray && !t.IsGenericType && t != typeof(UnityEngine.Object))
            {
                // Several targets need a T[] field; fall back to a single target when Udon has no T[] variable type.
                ParamType p = null;
                if (variableTypes.Contains(t.MakeArrayType())) p = ParamType.Objects(CSharpName(t));
                else if (variableTypes.Contains(t)) p = ParamType.Object(CSharpName(t));
                if (p != null) p.IsComponent = typeof(UnityEngine.Component).IsAssignableFrom(t);
                return p;
            }
            // Strings, structs and other reference types held in variables.
            return ValueType(t);
        }

        /// <summary>The generator type for a value of <paramref name="t"/>, or null when Udon cannot hold it.</summary>
        internal static ParamType ValueTypeOf(Type t)
        {
            Build();
            return ValueType(t);
        }

        /// <summary>
        /// An argument's type. Enums Udon has no variable type for (KeyCode, VRCPlayerApi.TrackingDataType...) are still
        /// fine as arguments when the assembler resolves them (a Type_ node is not enough: DefaultReflectionMode has one
        /// but fails to assemble): they are only ever constants (KeyCode.Space).
        /// </summary>
        static ParamType ArgumentType(Type t)
        {
            // The behaviour a call reports back to (VRCStringDownloader.LoadUrl, VRCTween's callbacks, network events
            // with values, Store.ListPurchases...): an UdonBehaviour, which implements it; this trigger by default.
            if (t != null && t.FullName == "VRC.Udon.Common.Interfaces.IUdonEventReceiver")
            {
                var receiver = ParamType.Object(ParamType.Behaviour);
                receiver.IsComponent = true;
                return receiver;
            }
            if (t != null && t.IsEnum && !t.IsGenericType && !variableTypes.Contains(t) && (t.IsPublic || t.IsNestedPublic)
                && UdonNodes.TypeFromUdonName(UdonTypeId(t)) == t) // the assembler's own lookup
                return new ParamType(ValueKind.Enum, CSharpName(t));
            return ValueType(t);
        }

        static ParamType ValueType(Type t)
        {
            // Every argument (constants included) lives in an Udon variable, so its type must be one Udon can hold.
            if (!variableTypes.Contains(t)) return null;
            if (t == typeof(bool)) return ParamType.Of(ValueKind.Bool);
            if (t == typeof(int)) return ParamType.Of(ValueKind.Int);
            if (t == typeof(float)) return ParamType.Of(ValueKind.Float);
            if (t == typeof(string)) return ParamType.Of(ValueKind.String);
            if (t == typeof(UnityEngine.Vector3)) return ParamType.Of(ValueKind.Vector3);
            if (t == typeof(UnityEngine.Vector2)) return ParamType.Of(ValueKind.Vector2);
            if (t == typeof(UnityEngine.Color)) return ParamType.Of(ValueKind.Color);
            if (t == typeof(UnityEngine.Quaternion)) return ParamType.Of(ValueKind.Quaternion);
            if (t == typeof(VRCPlayerApi)) return ParamType.Of(ValueKind.Player);
            if (t == typeof(VRCUrl)) return ParamType.Of(ValueKind.Url);
            if (t.IsEnum && !t.IsGenericType) return new ParamType(ValueKind.Enum, CSharpName(t));
            if (typeof(UnityEngine.Object).IsAssignableFrom(t) && !t.IsArray && !t.IsGenericType && variableTypes.Contains(t))
            {
                var p = ParamType.Object(CSharpName(t));
                p.IsComponent = typeof(UnityEngine.Component).IsAssignableFrom(t);
                return p;
            }
            if (t.IsGenericType || t.ContainsGenericParameters || t.IsPointer || t.IsByRef) return null;
            // Anything else Udon can hold (arrays, DataList, Vector4, System.Object, ...): passed through variables.
            return ParamType.OtherType(CSharpName(t), HasEqualityOperator(t));
        }
    }
}
