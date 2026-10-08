using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UdonBridge;

namespace UdonIde
{
    /// <summary>A place in a file (0-based line and column), with the line's text for lists.</summary>
    public struct SourcePlace
    {
        public string Path;
        public int Line, Column, Length;
        public string LineText;
    }

    /// <summary>Whether U# code can use a symbol.</summary>
    public enum Availability { NotApplicable, Udon, ProjectUdonSharp, NotExposed, Unknown }

    /// <summary>
    /// What the editor asks about a symbol: what it is, where it is defined, its documentation, whether Udon has it,
    /// and where it is used. Works on the check's compilation (all U# sources, unsaved tabs included).
    /// </summary>
    public static class SymbolNav
    {
        // ---- finding the symbol ----

        static readonly string[] EventMethods = { "SendCustomEvent", "SendCustomNetworkEvent", "SendCustomEventDelayedSeconds", "SendCustomEventDelayedFrames" };

        /// <summary>The symbol at an offset: an identifier, or the event name string of a SendCustomEvent call.</summary>
        public static ISymbol At(SemanticModel model, int offset)
        {
            var root = model.SyntaxTree.GetRoot();
            if (root.FullSpan.Length == 0) return null;
            offset = Math.Max(0, Math.Min(offset, root.FullSpan.End - 1));
            var token = root.FindToken(offset);
            if (!token.Span.Contains(offset) && offset > 0)
            {
                var before = root.FindToken(offset - 1);
                if (before.IsKind(SyntaxKind.IdentifierToken) || before.IsKind(SyntaxKind.StringLiteralToken)) token = before;
            }
            if (token.IsKind(SyntaxKind.StringLiteralToken))
            {
                var call = EventCallOf(token.Parent as LiteralExpressionSyntax);
                return call == null ? null : EventTargets(model, call.Value.invocation, token.ValueText).FirstOrDefault();
            }
            if (!token.IsKind(SyntaxKind.IdentifierToken) && !token.IsKind(SyntaxKind.ThisKeyword) && !token.IsKind(SyntaxKind.BaseKeyword)
                && !(token.Parent is PredefinedTypeSyntax)) return null;
            var node = token.Parent;
            var info = model.GetSymbolInfo(node);
            return info.Symbol ?? info.CandidateSymbols.FirstOrDefault() ?? model.GetDeclaredSymbol(node);
        }

        /// <summary>The SendCustomEvent-style call a string literal names the event of, if any.</summary>
        static (InvocationExpressionSyntax invocation, string method)? EventCallOf(LiteralExpressionSyntax literal)
        {
            if (literal?.Parent is ArgumentSyntax arg && arg.Parent?.Parent is InvocationExpressionSyntax inv)
            {
                var name = inv.Expression is MemberAccessExpressionSyntax ma ? ma.Name.Identifier.ValueText
                    : inv.Expression is IdentifierNameSyntax id ? id.Identifier.ValueText : null;
                if (name == null || !EventMethods.Contains(name)) return null;
                int index = ((ArgumentListSyntax)arg.Parent).Arguments.IndexOf(arg);
                if (index != (name == "SendCustomNetworkEvent" ? 1 : 0)) return null;
                return (inv, name);
            }
            return null;
        }

        /// <summary>The type a SendCustomEvent call is made on, when it is a U# behaviour of this project (null otherwise).</summary>
        static INamedTypeSymbol EventReceiver(SemanticModel model, InvocationExpressionSyntax inv)
        {
            ITypeSymbol type = inv.Expression is MemberAccessExpressionSyntax ma ? model.GetTypeInfo(ma.Expression).Type
                : model.GetEnclosingSymbol(inv.SpanStart)?.ContainingType;
            return type is INamedTypeSymbol named && IsProjectBehaviour(named) ? named : null;
        }

        static IEnumerable<IMethodSymbol> EventTargets(SemanticModel model, InvocationExpressionSyntax inv, string name)
        {
            var receiver = EventReceiver(model, inv);
            if (receiver == null) return Enumerable.Empty<IMethodSymbol>();
            return Hierarchy(receiver).SelectMany(t => t.GetMembers(name)).OfType<IMethodSymbol>();
        }

        static IEnumerable<INamedTypeSymbol> Hierarchy(INamedTypeSymbol t) { for (; t != null; t = t.BaseType) yield return t; }

        /// <summary>A class of this project's sources that derives from UdonSharpBehaviour.</summary>
        public static bool IsProjectBehaviour(INamedTypeSymbol t)
        {
            if (t == null || !t.Locations.Any(l => l.IsInSource)) return false;
            for (var b = t.BaseType; b != null; b = b.BaseType) if (b.ToDisplayString() == UdonSharpCheck.BehaviourTypeName) return true;
            return false;
        }

        // ---- what it is ----

        public static Availability Udon(ISymbol s, ITypeSymbol callSite = null)
        {
            if (s == null || s is ILocalSymbol || s is IParameterSymbol || s is ILabelSymbol || s is INamespaceSymbol || s is IRangeVariableSymbol) return Availability.NotApplicable;
            var type = s as INamedTypeSymbol ?? s.ContainingType;
            if (s.Locations.Any(l => l.IsInSource))
            {
                if (type != null && (IsProjectBehaviour(type) || type.TypeKind == TypeKind.Enum)) return Availability.ProjectUdonSharp;
                return Availability.NotExposed; // a plain class of the project: U# can't use it
            }
            if (IsUdonSharpAssembly(s.ContainingAssembly)) return Availability.Udon;
            if (s is INamedTypeSymbol) return Availability.NotApplicable;
            var exposed = UdonExposure.Exposed(s, false, callSite);
            return exposed == true ? Availability.Udon : exposed == false ? Availability.NotExposed : Availability.Unknown;
        }

        static string KindLabel(ISymbol s)
        {
            switch (s)
            {
                case IMethodSymbol m: return m.MethodKind == MethodKind.Constructor ? "コンストラクター" : "メソッド";
                case IPropertySymbol _: return "プロパティ";
                case IFieldSymbol f: return f.IsConst ? "定数" : f.ContainingType?.TypeKind == TypeKind.Enum ? "列挙値" : "フィールド";
                case ILocalSymbol _: return "ローカル変数";
                case IParameterSymbol _: return "引数";
                case IEventSymbol _: return "イベント";
                case INamedTypeSymbol t: return t.TypeKind == TypeKind.Enum ? "列挙型" : t.TypeKind == TypeKind.Interface ? "インターフェイス" : t.TypeKind == TypeKind.Struct ? "構造体" : "クラス";
                case INamespaceSymbol _: return "名前空間";
                default: return s.Kind.ToString();
            }
        }

        static readonly SymbolDisplayFormat Signature = SymbolDisplayFormat.MinimallyQualifiedFormat
            .WithMemberOptions(SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeType | SymbolDisplayMemberOptions.IncludeContainingType)
            .WithParameterOptions(SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeName | SymbolDisplayParameterOptions.IncludeDefaultValue);

        /// <summary>The hover text: kind and signature, what Udon makes of it, the documentation.</summary>
        public static string Describe(ISymbol s, CSharpCompilation compilation)
        {
            if (s == null) return null;
            var sb = new StringBuilder();
            sb.Append('(').Append(KindLabel(s)).Append(") ").Append(s.ToDisplayString(Signature));
            var def = Definition(s);
            switch (Udon(s))
            {
                case Availability.Udon: sb.Append("\nUdon で使えます"); break;
                case Availability.ProjectUdonSharp: sb.Append("\nU# スクリプト").Append(def != null ? "(" + def.Value.Path + ")" : ""); break;
                case Availability.NotExposed: sb.Append("\nUdon に公開されていないため、U# からは使えません"); break;
                case Availability.Unknown: sb.Append("\nUdon で使えるかどうか判定できません"); break;
            }
            var doc = Documentation(s, compilation);
            if (!string.IsNullOrEmpty(doc)) sb.Append("\n\n").Append(doc);
            return sb.ToString();
        }

        // ---- where it is ----

        public static SourcePlace? Definition(ISymbol s)
        {
            if (s == null) return null;
            if (s is IMethodSymbol m && m.PartialImplementationPart != null) s = m.PartialImplementationPart;
            var loc = s.Locations.FirstOrDefault(l => l.IsInSource);
            if (loc == null) return null;
            var span = loc.GetLineSpan();
            return new SourcePlace { Path = ProjectPaths.Relative(loc.SourceTree.FilePath), Line = span.StartLinePosition.Line, Column = span.StartLinePosition.Character, Length = loc.SourceSpan.Length };
        }

        /// <summary>UdonSharp's own assemblies (UdonSharpBehaviour's members, which UdonSharp maps itself).</summary>
        public static bool IsUdonSharpAssembly(IAssemblySymbol a) => a?.Name.StartsWith("UdonSharp", StringComparison.Ordinal) == true;

        /// <summary>Files of other people's packages: shown, not edited (the package manager would overwrite them).</summary>
        public static bool IsReadOnlyPath(string path) => path.Replace('\\', '/').StartsWith("Packages/", StringComparison.Ordinal) || path.Contains("/Library/PackageCache/");

        // ---- documentation ----

        /// <summary>The summary (and parameters) of a symbol: /// comments in source, the assembly's XML file otherwise.</summary>
        public static string Documentation(ISymbol s, CSharpCompilation compilation)
        {
            if (s == null) return null;
            if (s.Locations.Any(l => l.IsInSource)) return FromSource(s);
            var id = (s is IMethodSymbol m && m.ReducedFrom != null ? m.ReducedFrom : s.OriginalDefinition).GetDocumentationCommentId();
            if (id == null || s.ContainingAssembly == null) return null;
            var reference = compilation?.GetMetadataReference(s.ContainingAssembly) as PortableExecutableReference;
            var xmlPath = reference?.FilePath != null ? Path.ChangeExtension(reference.FilePath, ".xml") : null;
            if (xmlPath == null || !File.Exists(xmlPath)) return null;
            var docs = XmlDocs(xmlPath);
            return docs.TryGetValue(id, out var raw) ? Clean(raw) : null;
        }

        static string FromSource(ISymbol s)
        {
            var syntax = s.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
            if (syntax is VariableDeclaratorSyntax v) syntax = v.Parent?.Parent; // a field: the comments sit on the declaration
            if (syntax == null) return null;
            // DocumentationMode.None (as UdonSharp parses) leaves /// as plain comments: read them as text.
            var lines = syntax.GetLeadingTrivia().ToFullString().Split('\n').Select(l => l.Trim()).Where(l => l.StartsWith("///")).Select(l => l.Substring(3).Trim());
            var xml = string.Join("\n", lines);
            return xml.Length == 0 ? null : Clean(xml);
        }

        static readonly Dictionary<string, Dictionary<string, string>> xmlFiles = new Dictionary<string, Dictionary<string, string>>();
        static readonly object xmlGate = new object();

        static Dictionary<string, string> XmlDocs(string path)
        {
            lock (xmlGate)
            {
                if (xmlFiles.TryGetValue(path, out var d)) return d;
                d = new Dictionary<string, string>();
                try
                {
                    using (var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, IgnoreWhitespace = true }))
                    {
                        reader.MoveToContent();
                        while (!reader.EOF)
                        {
                            // ReadInnerXml leaves the reader on the next node already: read on only when it didn't.
                            if (reader.NodeType == XmlNodeType.Element && reader.Name == "member")
                            {
                                var name = reader.GetAttribute("name");
                                var inner = reader.ReadInnerXml();
                                if (name != null) d[name] = inner;
                            }
                            else reader.Read();
                        }
                    }
                }
                catch (Exception) { /* a broken doc file: no docs from it */ }
                xmlFiles[path] = d;
                return d;
            }
        }

        /// <summary>Loads an assembly's XML docs ahead of the first hover (background warm-up).</summary>
        public static void WarmDocs(string assemblyPath) { var x = Path.ChangeExtension(assemblyPath, ".xml"); if (File.Exists(x)) XmlDocs(x); }

        /// <summary>Doc XML to plain text: summary first, then parameters and return value.</summary>
        static string Clean(string xml)
        {
            string Section(string tag)
            {
                var m = Regex.Match(xml, $"<{tag}>(.*?)</{tag}>", RegexOptions.Singleline);
                return m.Success ? Text(m.Groups[1].Value) : null;
            }
            var parts = new List<string>();
            var summary = Section("summary") ?? (xml.Contains("<") ? null : Text(xml));
            if (!string.IsNullOrEmpty(summary)) parts.Add(summary);
            foreach (Match p in Regex.Matches(xml, "<param name=\"([^\"]+)\">(.*?)</param>", RegexOptions.Singleline))
                parts.Add(p.Groups[1].Value + ": " + Text(p.Groups[2].Value));
            var returns = Section("returns");
            if (!string.IsNullOrEmpty(returns)) parts.Add("戻り値: " + returns);
            return string.Join("\n", parts);
        }

        static string Text(string xml)
        {
            var t = Regex.Replace(xml, "<see(?:also)? (?:cref|langword|href)=\"(?:[A-Z]:)?([^\"]+)\"\\s*/>", m => m.Groups[1].Value.Split('.').Last());
            t = Regex.Replace(t, "<(?:paramref|typeparamref) name=\"([^\"]+)\"\\s*/>", "$1");
            t = Regex.Replace(t, "</?para>", "\n");
            t = Regex.Replace(t, "<[^>]+>", "");
            t = System.Net.WebUtility.HtmlDecode(t);
            t = Regex.Replace(t, "[ \t]*\n[ \t]*", "\n");
            t = Regex.Replace(t, "\n{2,}", "\n");
            t = Regex.Replace(t, "(?<!\n)\n(?!\n)", " ");
            return Regex.Replace(t, " {2,}", " ").Trim();
        }

        // ---- where it is used ----

        /// <summary>
        /// Uses of a symbol in all the compilation's files, and SendCustomEvent strings that name it. Only trees whose
        /// text contains the name are bound, so a search over a thousand files stays cheap.
        /// </summary>
        public static List<SourcePlace> References(CSharpCompilation compilation, ISymbol target, CancellationToken cancel = default)
        {
            var found = new List<SourcePlace>();
            if (target == null) return found;
            var original = target.OriginalDefinition;
            var name = target is IMethodSymbol ctor && ctor.MethodKind == MethodKind.Constructor ? ctor.ContainingType.Name : target.Name;
            foreach (var tree in compilation.SyntaxTrees)
            {
                cancel.ThrowIfCancellationRequested();
                var text = tree.GetText();
                if (text.ToString().IndexOf(name, StringComparison.Ordinal) < 0) continue;
                SemanticModel model = null;
                foreach (var token in tree.GetRoot().DescendantTokens())
                {
                    bool identifier = token.IsKind(SyntaxKind.IdentifierToken) && token.ValueText == name;
                    bool eventName = token.IsKind(SyntaxKind.StringLiteralToken) && token.ValueText == name && target is IMethodSymbol;
                    if (!identifier && !eventName) continue;
                    model ??= compilation.GetSemanticModel(tree);
                    ISymbol s;
                    if (eventName)
                    {
                        var call = EventCallOf(token.Parent as LiteralExpressionSyntax);
                        s = call == null ? null : EventTargets(model, call.Value.invocation, name).FirstOrDefault(m => SymbolEqualityComparer.Default.Equals(m.OriginalDefinition, original));
                    }
                    else
                    {
                        var info = model.GetSymbolInfo(token.Parent);
                        s = info.Symbol ?? info.CandidateSymbols.FirstOrDefault() ?? model.GetDeclaredSymbol(token.Parent);
                    }
                    if (s == null) continue;
                    var so = s.OriginalDefinition;
                    if (s is IMethodSymbol sm && sm.ReducedFrom != null) so = sm.ReducedFrom;
                    if (so is IMethodSymbol om && om.MethodKind == MethodKind.Constructor && target is INamedTypeSymbol) so = om.ContainingType;
                    if (!SymbolEqualityComparer.Default.Equals(so, original) && !Overrides(so, original)) continue;
                    var pos = tree.GetLineSpan(token.Span).StartLinePosition;
                    found.Add(new SourcePlace { Path = ProjectPaths.Relative(tree.FilePath), Line = pos.Line, Column = pos.Character, Length = token.Span.Length,
                        LineText = text.Lines[pos.Line].ToString().Trim() });
                }
            }
            return found.OrderBy(f => f.Path, StringComparer.Ordinal).ThenBy(f => f.Line).ToList();
        }

        // An override counts as a use of the method it overrides (and the other way round).
        static bool Overrides(ISymbol a, ISymbol b)
        {
            for (var m = a as IMethodSymbol; m != null; m = m.OverriddenMethod) if (SymbolEqualityComparer.Default.Equals(m.OriginalDefinition, b)) return true;
            for (var m = b as IMethodSymbol; m != null; m = m.OverriddenMethod) if (SymbolEqualityComparer.Default.Equals(m.OriginalDefinition, a)) return true;
            return false;
        }

        // ---- SendCustomEvent names ----

        /// <summary>
        /// SendCustomEvent names that won't reach a method: no such method, not public, or (over the network) a name
        /// starting with "_". UdonSharp doesn't check the strings, so a typo only shows as nothing happening at run time.
        /// Calls on a plain UdonBehaviour are skipped: the target isn't known.
        /// </summary>
        public static List<Problem> EventNameProblems(SemanticModel model)
        {
            var problems = new List<Problem>();
            var tree = model.SyntaxTree;
            foreach (var literal in tree.GetRoot().DescendantNodes().OfType<LiteralExpressionSyntax>())
            {
                if (!literal.IsKind(SyntaxKind.StringLiteralExpression)) continue;
                var call = EventCallOf(literal);
                if (call == null) continue;
                var receiver = EventReceiver(model, call.Value.invocation);
                if (receiver == null) continue;
                var name = literal.Token.ValueText;
                var methods = Hierarchy(receiver).TakeWhile(t => t.ToDisplayString() != UdonSharpCheck.BehaviourTypeName).SelectMany(t => t.GetMembers(name)).OfType<IMethodSymbol>().ToList();
                string message = null;
                if (methods.Count == 0)
                {
                    var near = Hierarchy(receiver).TakeWhile(t => t.ToDisplayString() != UdonSharpCheck.BehaviourTypeName).SelectMany(t => t.GetMembers()).OfType<IMethodSymbol>()
                        .Where(m => m.MethodKind == MethodKind.Ordinary && m.DeclaredAccessibility == Accessibility.Public)
                        .Select(m => m.Name).Distinct().OrderBy(n => Distance(n, name)).FirstOrDefault(n => Distance(n, name) <= 2);
                    message = $"{receiver.Name} に「{name}」というメソッドはありません。" + (near != null ? $"「{near}」の打ち間違いかもしれません。" : "") + "SendCustomEvent は名前が違っても実行時に何も起きないだけなので、nameof を使うと確実です。";
                }
                else if (!methods.Any(m => m.DeclaredAccessibility == Accessibility.Public))
                    message = $"「{name}」は public ではないので、SendCustomEvent では呼べません。";
                else if (call.Value.method == "SendCustomNetworkEvent" && name.StartsWith("_", StringComparison.Ordinal))
                    message = $"「{name}」は _ で始まるので、ネットワーク越しには呼べません(自分の環境だけで動きます)。";
                if (message == null) continue;
                var pos = literal.GetLocation().GetLineSpan().StartLinePosition;
                problems.Add(new Problem { Start = literal.SpanStart, Length = literal.Span.Length, Line = pos.Line, Column = pos.Character, Error = false, Source = "IDE", Message = message });
            }
            return problems;
        }

        static int Distance(string a, string b)
        {
            var d = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= b.Length; j++) d[0, j] = j;
            for (int i = 1; i <= a.Length; i++)
                for (int j = 1; j <= b.Length; j++)
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (char.ToLowerInvariant(a[i - 1]) == char.ToLowerInvariant(b[j - 1]) ? 0 : 1));
            return d[a.Length, b.Length];
        }
    }
}
