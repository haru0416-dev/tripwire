using System;
using System.Linq;
using System.Reflection;
using UdonSharp.Compiler;

namespace UdonBridge
{
    /// <summary>
    /// UdonSharp's extern names for members, from its internal naming code. Inherited members are named for the type
    /// they are called on (SystemArray.__Equals, not SystemObject), fields through get_ / set_ (ExposureAgreement test).
    /// </summary>
    public static class UdonNames
    {
        static readonly Type Interface = typeof(UdonSharpCompilerV1).Assembly.GetType("UdonSharp.Compiler.Udon.CompilerUdonInterface");
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        static readonly MethodInfo cacheInit = Interface?.GetMethod("CacheInit", Any);
        static readonly MethodInfo methodName = Interface?.GetMethods(Any).FirstOrDefault(m => m.Name == "GetUdonMethodName" && Takes(m, typeof(MethodBase), typeof(Type)));
        static readonly MethodInfo accessorName = Interface?.GetMethods(Any).FirstOrDefault(m => m.Name == "GetUdonAccessorName" && Takes(m, typeof(FieldInfo), null, typeof(Type)));
        static readonly Type accessorKind = accessorName?.GetParameters()[1].ParameterType;

        static bool Takes(MethodInfo m, params Type[] types) { var ps = m.GetParameters(); return ps.Length == types.Length && ps.Zip(types, (p, t) => t == null || p.ParameterType == t).All(x => x); }
        static bool ready;

        public static bool Available => cacheInit != null && methodName != null && accessorName != null && accessorKind != null;

        /// <summary>Loads the naming caches on the main thread (they read the AssetDatabase).</summary>
        public static void Warm() => Init();

        static void Init()
        {
            if (ready) return;
            if (!Available) throw new InvalidOperationException("UdonSharp's naming code was not found (CompilerUdonInterface changed in this SDK)");
            cacheInit.Invoke(null, null);
            ready = true;
        }

        /// <summary>The extern name of a method, constructor or property accessor, called on <paramref name="callSite"/> (default: its declaring type).</summary>
        public static string Of(MethodBase member, Type callSite = null)
        {
            Init();
            var over = callSite != null && callSite != member.DeclaringType ? callSite : null;
            return (string)methodName.Invoke(null, new object[] { member, over });
        }

        /// <summary>The extern name for reading (or writing) a field, on <paramref name="callSite"/> (default: its declaring type).</summary>
        public static string Of(FieldInfo field, bool write, Type callSite = null)
        {
            Init();
            var over = callSite != null && callSite != field.DeclaringType ? callSite : null;
            return (string)accessorName.Invoke(null, new object[] { field, Enum.ToObject(accessorKind, write ? 1 : 0), over });
        }

        public static bool IsExposed(MethodBase member, Type callSite = null) => UdonNodes.IsExposed(Of(member, callSite));
        public static bool IsExposed(FieldInfo field, bool write, Type callSite = null) => UdonNodes.IsExposed(Of(field, write, callSite));
    }
}
