using System;
using System.Collections.Generic;
using System.Linq;
using VRC.Udon.Editor;
using VRC.Udon.Graph;

namespace UdonBridge
{
    /// <summary>Udon's node definitions: what Udon exposes (UdonSharp's exposure check reads the same list).</summary>
    public static class UdonNodes
    {
        static List<UdonNodeDefinition> all;
        static HashSet<string> names;
        static HashSet<Type> resolvable, variableTypes, equalityTypes;

        static void Build()
        {
            if (all != null) return;
            // Published last: a thread that sees `all` sees the rest.
            var defs = UdonEditorManager.Instance.GetNodeDefinitions().ToList();
            var resolvableTypes = new HashSet<Type>(defs.Where(d => d.fullName.StartsWith("Type_", StringComparison.Ordinal) && d.type != null).Select(d => d.type));
            names = new HashSet<string>(defs.Select(d => d.fullName), StringComparer.Ordinal);
            resolvable = resolvableTypes;
            // Variable_ nodes list the types a variable may have; the assembler also needs the type resolvable (Type_
            // nodes). A few UI event types (Button.ButtonClickedEvent, ...) are in the first list only and fail to assemble.
            variableTypes = new HashSet<Type>(defs.Where(d => d.fullName.StartsWith("Variable_", StringComparison.Ordinal) && d.type != null && resolvableTypes.Contains(d.type)).Select(d => d.type));
            equalityTypes = new HashSet<Type>(defs.Where(d => d.fullName.Contains(".__op_Equality__") && d.type != null).Select(d => d.type));
            System.Threading.Volatile.Write(ref all, defs);
        }

        public static IReadOnlyList<UdonNodeDefinition> All { get { Build(); return all; } }

        public static bool IsExposed(string udonName) { Build(); return udonName != null && names.Contains(udonName); }

        /// <summary>Types the Udon assembler can resolve (Type_ nodes): constants of these can appear in a program.</summary>
        public static IReadOnlyCollection<Type> ResolvableTypes { get { Build(); return resolvable; } }

        public static IReadOnlyCollection<Type> VariableTypes { get { Build(); return variableTypes; } }

        public static IReadOnlyCollection<Type> EqualityTypes { get { Build(); return equalityTypes; } }

        public static Type TypeFromUdonName(string udonTypeName) => UdonEditorManager.Instance.GetTypeFromTypeString(udonTypeName);

        /// <summary>Event_ nodes; their outputs are the event's parameters.</summary>
        public static IEnumerable<UdonNodeDefinition> Events => UdonEditorManager.Instance.GetNodeDefinitions("Event_");
    }
}
