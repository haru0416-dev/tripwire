using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UdonBridge;

namespace UdonIde
{
    /// <summary>Members the code uses that Udon doesn't expose (when UdonSharp's own check is unavailable or off).</summary>
    public static class UdonExposure
    {
        // Looked up by the worker and the main thread alike.
        static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Type> types = new System.Collections.Concurrent.ConcurrentDictionary<string, Type>();

        public static void Warm() { _ = UdonNodes.All; if (UdonNames.Available) UdonNames.Warm(); }

        static Type TypeOf(INamedTypeSymbol t)
        {
            var name = t.ContainingType != null ? TypeOf(t.ContainingType)?.FullName + "+" + t.MetadataName
                : (t.ContainingNamespace.IsGlobalNamespace ? "" : t.ContainingNamespace.ToDisplayString() + ".") + t.MetadataName;
            return types.GetOrAdd(name, n => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(n)).FirstOrDefault(x => x != null));
        }

        static bool SameParams(ParameterInfo[] ps, IMethodSymbol m) =>
            ps.Length == m.Parameters.Length && ps.Select((p, i) => p.ParameterType.Name.TrimEnd('&') == ((m.Parameters[i].Type as IArrayTypeSymbol) != null
                ? ((IArrayTypeSymbol)m.Parameters[i].Type).ElementType.MetadataName + "[]" : m.Parameters[i].Type.MetadataName)).All(x => x);

        /// <summary>Null when it can't tell (generics, types not loaded).</summary>
        public static bool? Exposed(ISymbol symbol, bool write = false, ITypeSymbol callSite = null)
        {
            if (symbol == null || !UdonNames.Available) return null;
            var member = Member(symbol, write, out var field);
            if (member == null && field == null) return null;
            var site = callSite is INamedTypeSymbol named ? TypeOf(named.OriginalDefinition) : null;
            return field != null ? UdonNames.IsExposed(field, write, site) : UdonNames.IsExposed(member, site);
        }

        static MethodBase Member(ISymbol symbol, bool write, out FieldInfo field)
        {
            field = null;
            var type = symbol.ContainingType == null ? null : TypeOf(symbol.ContainingType.OriginalDefinition);
            if (type == null) return null;
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            switch (symbol)
            {
                case IMethodSymbol m when m.MethodKind == MethodKind.Constructor:
                    return type.GetConstructors(all).FirstOrDefault(c => SameParams(c.GetParameters(), m));
                case IMethodSymbol m when !m.IsGenericMethod:
                    return type.GetMethods(all).FirstOrDefault(x => x.Name == m.Name && SameParams(x.GetParameters(), m));
                case IPropertySymbol p:
                    var prop = type.GetProperty(p.MetadataName, all);
                    return write ? prop?.GetSetMethod(true) : prop?.GetGetMethod(true);
                case IFieldSymbol f when !f.IsConst:
                    field = type.GetField(f.MetadataName, all);
                    return null;
                default: return null;
            }
        }

        public static List<Problem> Check(SemanticModel model, SyntaxTree tree)
        {
            var problems = new List<Problem>();
            foreach (var node in tree.GetRoot().DescendantNodes())
            {
                if (!(node is InvocationExpressionSyntax || node is ObjectCreationExpressionSyntax || node is MemberAccessExpressionSyntax || node is IdentifierNameSyntax)) continue;
                if (node is IdentifierNameSyntax && node.Parent is MemberAccessExpressionSyntax ma && ma.Name == node) continue; // the member access reports it
                var symbol = model.GetSymbolInfo(node).Symbol;
                if (symbol == null || symbol.ContainingType == null) continue;
                if (symbol.Locations.Any(l => l.IsInSource)) continue; // the script's own members
                if (SymbolNav.IsUdonSharpAssembly(symbol.ContainingAssembly)) continue; // UdonSharpBehaviour's members: UdonSharp maps them itself
                if (symbol is IMethodSymbol im && (im.MethodKind == MethodKind.PropertyGet || im.MethodKind == MethodKind.PropertySet)) continue;
                if (node is InvocationExpressionSyntax) continue; // its MemberAccess / Identifier child carries the method
                bool write = node.Parent is AssignmentExpressionSyntax assign && assign.Left == node;
                var site = node is MemberAccessExpressionSyntax access ? model.GetTypeInfo(access.Expression).Type : null;
                if (Exposed(symbol, write, site) != false) continue;
                var span = node is MemberAccessExpressionSyntax m2 ? m2.Name.Span : node.Span;
                var pos = tree.GetLineSpan(span).StartLinePosition;
                problems.Add(new Problem { Start = span.Start, Length = span.Length, Line = pos.Line, Column = pos.Character, Error = true,
                    Source = "Udon", Message = symbol.ToDisplayString() + " is not exposed to Udon" });
            }
            return problems;
        }
    }
}
