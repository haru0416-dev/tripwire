using System.Collections.Generic;

namespace UdonIde.Editing
{
    public enum TokenKind : byte { Plain, Keyword, Type, String, Number, Comment, Preprocessor }

    /// <summary>A colored run in a line: [Start, Start + Length).</summary>
    public struct Token
    {
        public int Start, Length;
        public TokenKind Kind;
    }

    /// <summary>What a line starts inside of: block comments and verbatim strings run across lines.</summary>
    public enum LexState : byte { Normal, BlockComment, VerbatimString }

    /// <summary>
    /// Colors one line at a time, given the state the previous line ended in. The view colors only the lines on
    /// screen; <see cref="LineStates"/> keeps each line's start state so a jump to line 1500 doesn't lex from the top
    /// more than once.
    /// </summary>
    public static class LineLexer
    {
        static readonly HashSet<string> Keywords = new HashSet<string>
        {
            "using", "namespace", "public", "private", "protected", "internal", "class", "struct", "enum", "static", "readonly",
            "const", "void", "return", "if", "else", "for", "foreach", "while", "do", "break", "continue", "new", "this", "base",
            "true", "false", "null", "override", "virtual", "abstract", "sealed", "in", "out", "ref", "is", "as", "switch", "case",
            "default", "get", "set", "var", "typeof", "nameof", "partial", "params", "try", "catch", "finally", "throw", "goto",
            "checked", "unchecked", "event", "operator", "delegate", "async", "await", "yield", "where", "lock", "value", "interface",
        };
        static readonly HashSet<string> Types = new HashSet<string>
        {
            "bool", "int", "float", "double", "string", "object", "byte", "sbyte", "long", "ulong", "uint", "ushort", "char", "decimal", "short",
        };

        public static LexState Lex(string line, LexState state, List<Token> tokens)
        {
            tokens?.Clear();
            int i = 0, n = line.Length;
            void Add(TokenKind k, int from, int to) { if (to > from) tokens?.Add(new Token { Start = from, Length = to - from, Kind = k }); }

            if (state == LexState.BlockComment)
            {
                int end = line.IndexOf("*/", System.StringComparison.Ordinal);
                if (end < 0) { Add(TokenKind.Comment, 0, n); return LexState.BlockComment; }
                i = end + 2; Add(TokenKind.Comment, 0, i);
            }
            else if (state == LexState.VerbatimString)
            {
                i = VerbatimEnd(line, 0);
                if (i < 0) { Add(TokenKind.String, 0, n); return LexState.VerbatimString; }
                Add(TokenKind.String, 0, i);
            }

            // A preprocessor line (#if, #region...) is one run.
            int firstNonBlank = 0; while (firstNonBlank < n && char.IsWhiteSpace(line[firstNonBlank])) firstNonBlank++;
            if (i == 0 && firstNonBlank < n && line[firstNonBlank] == '#') { Add(TokenKind.Preprocessor, firstNonBlank, n); return LexState.Normal; }

            while (i < n)
            {
                char c = line[i];
                int start = i;
                if (c == '/' && i + 1 < n && line[i + 1] == '/') { Add(TokenKind.Comment, i, n); return LexState.Normal; }
                if (c == '/' && i + 1 < n && line[i + 1] == '*')
                {
                    int end = line.IndexOf("*/", i + 2, System.StringComparison.Ordinal);
                    if (end < 0) { Add(TokenKind.Comment, i, n); return LexState.BlockComment; }
                    i = end + 2; Add(TokenKind.Comment, start, i); continue;
                }
                if (c == '@' && i + 1 < n && line[i + 1] == '"' || c == '$' && i + 2 < n && line[i + 1] == '@' && line[i + 2] == '"' || c == '@' && i + 2 < n && line[i + 1] == '$' && line[i + 2] == '"')
                {
                    int open = line.IndexOf('"', i);
                    int end = VerbatimEnd(line, open + 1);
                    if (end < 0) { Add(TokenKind.String, i, n); return LexState.VerbatimString; }
                    i = end; Add(TokenKind.String, start, i); continue;
                }
                if (c == '"' || c == '\'' || c == '$' && i + 1 < n && line[i + 1] == '"')
                {
                    char quote = c == '$' ? '"' : c;
                    i += c == '$' ? 2 : 1;
                    while (i < n && line[i] != quote) { if (line[i] == '\\') i++; i++; }
                    i = System.Math.Min(n, i + 1);
                    Add(TokenKind.String, start, i); continue;
                }
                if (char.IsDigit(c) || c == '.' && i + 1 < n && char.IsDigit(line[i + 1]))
                {
                    while (i < n && (char.IsLetterOrDigit(line[i]) || line[i] == '.' || line[i] == '_')) i++;
                    Add(TokenKind.Number, start, i); continue;
                }
                if (char.IsLetter(c) || c == '_' || c == '@' && i + 1 < n && char.IsLetter(line[i + 1]))
                {
                    i++;
                    while (i < n && (char.IsLetterOrDigit(line[i]) || line[i] == '_')) i++;
                    var word = line.Substring(start, i - start);
                    var kind = Keywords.Contains(word) ? TokenKind.Keyword : Types.Contains(word) ? TokenKind.Type
                        : c < 128 && char.IsUpper(c) ? TokenKind.Type : TokenKind.Plain;
                    Add(kind, start, i); continue;
                }
                int plainStart = i;
                while (i < n && !IsTokenStart(line, i)) i++;
                if (i == plainStart) i++;
                Add(TokenKind.Plain, plainStart, i);
            }
            return LexState.Normal;
        }

        static bool IsTokenStart(string line, int i)
        {
            char c = line[i];
            return char.IsLetterOrDigit(c) || c == '_' || c == '"' || c == '\'' || c == '@' || c == '$' || c == '/' || c == '.' && i + 1 < line.Length && char.IsDigit(line[i + 1]);
        }

        /// <summary>The index after the closing quote of a verbatim string ("" is an escaped quote), or -1 when it runs on.</summary>
        static int VerbatimEnd(string line, int from)
        {
            int i = from;
            while (i < line.Length)
            {
                if (line[i] == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { i += 2; continue; }
                    return i + 1;
                }
                i++;
            }
            return -1;
        }
    }

    /// <summary>The start state of each line, worked out lazily and dropped from the first changed line on.</summary>
    public sealed class LineStates
    {
        readonly List<LexState> starts = new List<LexState> { LexState.Normal };

        public void Invalidate(int fromLine)
        {
            int keep = System.Math.Max(1, fromLine + 1);
            if (starts.Count > keep) starts.RemoveRange(keep, starts.Count - keep);
        }

        public LexState StartOf(TextBuffer buffer, int line)
        {
            while (starts.Count <= line && starts.Count <= buffer.LineCount)
            {
                int l = starts.Count - 1;
                starts.Add(LineLexer.Lex(buffer.Lines[l], starts[l], null));
            }
            return starts[System.Math.Min(line, starts.Count - 1)];
        }
    }
}
