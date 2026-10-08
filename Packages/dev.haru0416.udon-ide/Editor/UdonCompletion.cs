using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UdonIde
{
    public enum CompletionKind { Method, Property, Field, EnumMember, Local, Namespace, Event, Type, Keyword }

    public sealed class Completion
    {
        public string Name, Detail;
        public CompletionKind Kind;
        /// <summary>U# can use it.</summary>
        public bool Exposed;
        public ISymbol Symbol;
        /// <summary>Lower first among equal matches.</summary>
        public int Rank;
    }

    public sealed class CompletionList
    {
        public List<Completion> Items = new List<Completion>();
        public int WordStart;
    }

    /// <summary>What U# can use comes first; the rest stays listed, dimmed.</summary>
    public static class UdonCompletion
    {
        static readonly string[] Keywords =
        {
            "if", "else", "for", "foreach", "while", "do", "return", "break", "continue", "switch", "case", "default", "new", "this", "base",
            "true", "false", "null", "var", "public", "private", "protected", "static", "readonly", "const", "override", "virtual", "void",
            "int", "float", "bool", "string", "nameof", "typeof", "is", "as", "in", "out", "ref", "using", "class", "enum", "goto",
        };

        public static CompletionList At(CSharpCompilation compilation, SyntaxTree tree, int offset)
        {
            var list = new CompletionList();
            var text = tree.GetText().ToString();
            offset = System.Math.Max(0, System.Math.Min(offset, text.Length));
            int start = offset;
            while (start > 0 && (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] == '_')) start--;
            list.WordStart = start;
            var model = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();

            if (offset > 0)
            {
                var trivia = root.FindTrivia(offset - 1);
                if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)) return list;
            }

            int dot = start - 1;
            while (dot >= 0 && (text[dot] == ' ' || text[dot] == '\t')) dot--;
            if (dot >= 0 && text[dot] == '.')
            {
                var token = root.FindToken(dot);
                // "x." can parse as a member access or, before a declaration on the next line ("x.\n var y = ..."), as
                // the qualified type "x.var": either way the left side is bound again as an expression.
                ExpressionSyntax left = (token.Parent as MemberAccessExpressionSyntax ?? token.Parent?.Parent as MemberAccessExpressionSyntax)?.Expression;
                if (left == null && (token.Parent as QualifiedNameSyntax ?? token.Parent?.Parent as QualifiedNameSyntax) is QualifiedNameSyntax q) left = q.Left;
                if (left == null) return list;
                var asExpression = SyntaxFactory.ParseExpression(left.ToString());
                var leftSymbol = model.GetSpeculativeSymbolInfo(left.SpanStart, asExpression, SpeculativeBindingOption.BindAsExpression).Symbol
                                 ?? model.GetSymbolInfo(left).Symbol;
                if (leftSymbol is INamespaceSymbol ns)
                {
                    foreach (var m in ns.GetMembers().Where(m => m.CanBeReferencedByName)) list.Items.Add(Item(new List<ISymbol> { m }, 0));
                }
                else
                {
                    var asType = leftSymbol as ITypeSymbol;                     // "Networking." : static members
                    var type = asType ?? model.GetSpeculativeTypeInfo(left.SpanStart, asExpression, SpeculativeBindingOption.BindAsExpression).Type
                               ?? model.GetTypeInfo(left).Type;                // "transform." : instance members
                    if (type == null || type.TypeKind == TypeKind.Error) return list;
                    bool wantStatic = asType != null;
                    var members = model.LookupSymbols(offset, type).Where(s => s.IsStatic == wantStatic || (wantStatic && s is INamedTypeSymbol));
                    foreach (var g in members.Where(Listable).GroupBy(s => s.Name)) list.Items.Add(Item(g.ToList(), MemberRank(g.First()), type));
                }
            }
            else
            {
                var enclosing = model.GetEnclosingSymbol(offset);
                var inScope = model.LookupSymbols(offset).Where(s => Listable(s) || s is INamespaceSymbol || s is ILocalSymbol || s is IParameterSymbol);
                foreach (var g in inScope.GroupBy(s => s.Name))
                {
                    var first = g.First();
                    int rank = first is ILocalSymbol || first is IParameterSymbol ? 0
                        : SymbolEqualityComparer.Default.Equals(first.ContainingType, enclosing?.ContainingType) ? 1
                        : first is INamedTypeSymbol || first is INamespaceSymbol ? 3 : 2;
                    list.Items.Add(Item(g.ToList(), rank));
                }
                foreach (var k in Keywords) list.Items.Add(new Completion { Name = k, Kind = CompletionKind.Keyword, Detail = "", Exposed = true, Rank = 2 });
            }
            list.Items = list.Items.GroupBy(i => i.Name).Select(g => g.OrderByDescending(i => i.Exposed).ThenBy(i => i.Rank).First())
                .OrderBy(i => i.Exposed ? 0 : 1).ThenBy(i => i.Rank).ThenBy(i => i.Name, System.StringComparer.OrdinalIgnoreCase).ToList();
            return list;
        }

        static readonly HashSet<string> ObjectMembers = new HashSet<string> { "Equals", "GetHashCode", "GetType", "ToString", "ReferenceEquals", "GetInstanceID", "MemberwiseClone" };

        // After a dot: own members first, UdonSharp's next, inherited ones last.
        static int MemberRank(ISymbol s)
        {
            if (ObjectMembers.Contains(s.Name)) return 4;
            if (s.Locations.Any(l => l.IsInSource)) return 0;
            if (SymbolNav.IsUdonSharpAssembly(s.ContainingAssembly)) return 2;
            return 3;
        }

        static bool Listable(ISymbol s)
        {
            if (!s.CanBeReferencedByName) return false;
            if (s is IMethodSymbol m) return m.MethodKind == MethodKind.Ordinary || m.MethodKind == MethodKind.ReducedExtension;
            return s is IPropertySymbol || s is IFieldSymbol || s is IEventSymbol || s is INamedTypeSymbol;
        }

        static Completion Item(List<ISymbol> overloads, int rank, ITypeSymbol callSite = null)
        {
            var s = overloads[0];
            var avail = SymbolNav.Udon(s, callSite);
            // Among overloads, show one U# can use when there is one.
            if (avail != Availability.Udon && avail != Availability.ProjectUdonSharp && overloads.Count > 1)
            {
                var usable = overloads.FirstOrDefault(o => SymbolNav.Udon(o, callSite) == Availability.Udon);
                if (usable != null) { s = usable; avail = Availability.Udon; }
            }
            var kind = s is IMethodSymbol ? CompletionKind.Method : s is IPropertySymbol ? CompletionKind.Property
                : s is IFieldSymbol f ? (f.ContainingType?.TypeKind == TypeKind.Enum ? CompletionKind.EnumMember : CompletionKind.Field)
                : s is ILocalSymbol || s is IParameterSymbol ? CompletionKind.Local : s is INamespaceSymbol ? CompletionKind.Namespace
                : s is IEventSymbol ? CompletionKind.Event : CompletionKind.Type;
            var detail = s is INamedTypeSymbol || s is INamespaceSymbol ? s.ToDisplayString() : s.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            if (overloads.Count > 1) detail += $" (+{overloads.Count - 1})";
            return new Completion
            {
                Name = s.Name, Kind = kind, Detail = detail, Symbol = s, Rank = rank,
                Exposed = avail == Availability.Udon || avail == Availability.ProjectUdonSharp || avail == Availability.NotApplicable,
            };
        }

        /// <summary>Dev: completions after the dot at <paramref name="dotPosition"/>.</summary>
        internal static List<Completion> After(string text, int dotPosition, string path)
        {
            var (compilation, tree) = UdonCheck.Compile(path, text);
            return At(compilation, tree, dotPosition + 1).Items;
        }
    }
}
