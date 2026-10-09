using System;
using System.Collections.Generic;

namespace Tripwire.Core
{
    /// <summary>
    /// Rules shared by the generator, the Inspector and the tests, so they cannot drift apart: how actions are
    /// numbered, and which variables a parameter can name.
    /// </summary>
    public static class ActionRules
    {
        /// <summary>
        /// Actions in numbering order (diagnostics, bindings): each action, then a block's Then actions, then its
        /// Else actions, depth first. Contents of non-block actions are ignored. Works on the editor's data too.
        /// </summary>
        public static List<T> Flatten<T>(IEnumerable<T> actions, Func<T, string> id, Func<T, IEnumerable<T>> then, Func<T, IEnumerable<T>> otherwise) where T : class
        {
            var all = new List<T>();
            void Walk(IEnumerable<T> list)
            {
                foreach (var a in list)
                {
                    if (a == null) continue;
                    all.Add(a);
                    if (!ActionCatalog.IsBlock(id(a))) continue;
                    Walk(then(a));
                    Walk(otherwise(a));
                }
            }
            Walk(actions);
            return all;
        }

        /// <summary>
        /// Whether a variable of this type fits a variable-reference parameter. listType is the type of the list a
        /// For Each walks (for its item variable). The generator reports the same cases as errors.
        /// </summary>
        public static bool VariableFits(ActionSpec spec, ActionParam prm, ParamType type, bool synced, ParamType listType = null)
        {
            bool single = type != null && !type.IsArray;
            switch (prm.Role)
            {
                case VariableRole.Counter:
                    return single && type.Kind == ValueKind.Int && !synced;
                case VariableRole.List:
                    return type != null && CodeGenerator.ElementTypeOf(type) != null;
                case VariableRole.Item:
                    var element = CodeGenerator.ElementTypeOf(listType);
                    return type != null && element != null && !synced && CodeGenerator.IsAssignable(type, element);
            }
            switch (spec.Special)
            {
                case ActionSpecial.ToggleVariable:
                    return type.Kind == ValueKind.Bool && single;
                case ActionSpecial.AddVariable:
                case ActionSpecial.RandomVariable:
                    return (type.Kind == ValueKind.Int || type.Kind == ValueKind.Float) && single;
                case ActionSpecial.Calculate:
                    return single && CodeGenerator.Calculable(type.Kind);
                case ActionSpecial.GetComponent:
                    return single && type.Kind == ValueKind.Object && type.IsComponent;
                case ActionSpecial.GetRemoteVariable:
                    // listType: the other trigger's variable being read.
                    return listType == null || (type != null && CodeGenerator.IsAssignable(type, listType));
                default:
                    return true; // any variable, even one whose type can't be resolved (the generator reports that)
            }
        }
    }
}
