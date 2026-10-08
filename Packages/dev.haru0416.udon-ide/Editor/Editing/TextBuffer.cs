using System;
using System.Collections.Generic;
using System.Text;

namespace UdonIde.Editing
{
    /// <summary>A place in the text: line and column (both 0-based; the column counts UTF-16 chars).</summary>
    public struct TextPos : IComparable<TextPos>, IEquatable<TextPos>
    {
        public int Line, Col;
        public TextPos(int line, int col) { Line = line; Col = col; }
        public int CompareTo(TextPos o) => Line != o.Line ? Line.CompareTo(o.Line) : Col.CompareTo(o.Col);
        public bool Equals(TextPos o) => Line == o.Line && Col == o.Col;
        public override bool Equals(object o) => o is TextPos p && Equals(p);
        public override int GetHashCode() => Line * 7919 + Col;
        public static bool operator <(TextPos a, TextPos b) => a.CompareTo(b) < 0;
        public static bool operator >(TextPos a, TextPos b) => a.CompareTo(b) > 0;
        public static bool operator <=(TextPos a, TextPos b) => a.CompareTo(b) <= 0;
        public static bool operator >=(TextPos a, TextPos b) => a.CompareTo(b) >= 0;
        public static bool operator ==(TextPos a, TextPos b) => a.Equals(b);
        public static bool operator !=(TextPos a, TextPos b) => !a.Equals(b);
        public override string ToString() => $"{Line}:{Col}";
    }

    /// <summary>
    /// The text being edited, as lines, with the caret, the selection and undo. Every change goes through
    /// <see cref="Replace"/>, which records it for undo. No Unity here: the view turns keys and clicks into these calls.
    /// </summary>
    public sealed class TextBuffer
    {
        public const int TabSize = 4;

        readonly List<string> lines = new List<string> { "" };
        public IReadOnlyList<string> Lines => lines;
        public int LineCount => lines.Count;

        /// <summary>The caret, and the other end of the selection (equal to the caret when nothing is selected).</summary>
        public TextPos Caret, Anchor;
        /// <summary>The column the caret wants on up/down moves (kept across short lines).</summary>
        int wantedCol = -1;

        /// <summary>Goes up on every change of the text.</summary>
        public int Version { get; private set; }
        /// <summary>The first line changed since <see cref="TakeChangedFrom"/> was last called (int.MaxValue: none).</summary>
        int changedFrom = int.MaxValue;

        /// <summary>Time source for grouping typed characters into one undo step (seconds).</summary>
        public Func<double> Clock = () => DateTime.UtcNow.Subtract(DateTime.MinValue).TotalSeconds;

        public bool HasSelection => Caret != Anchor;
        public TextPos SelStart => Caret < Anchor ? Caret : Anchor;
        public TextPos SelEnd => Caret < Anchor ? Anchor : Caret;

        public string Text
        {
            get => string.Join("\n", lines);
            set
            {
                lines.Clear();
                lines.AddRange(Split(value ?? ""));
                Caret = Anchor = default;
                wantedCol = -1;
                undo.Clear(); redo.Clear();
                Changed(0);
            }
        }

        static string[] Split(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        public int TakeChangedFrom() { int c = changedFrom; changedFrom = int.MaxValue; return c; }
        void Changed(int line) { Version++; changedFrom = Math.Min(changedFrom, line); }

        public TextPos Clamp(TextPos p)
        {
            int line = Math.Max(0, Math.Min(lines.Count - 1, p.Line));
            int col = Math.Max(0, Math.Min(lines[line].Length, p.Col));
            if (col > 0 && col < lines[line].Length && char.IsLowSurrogate(lines[line][col]) && char.IsHighSurrogate(lines[line][col - 1])) col--;
            return new TextPos(line, col);
        }

        public TextPos End => new TextPos(lines.Count - 1, lines[lines.Count - 1].Length);

        public string GetText(TextPos from, TextPos to)
        {
            if (to < from) (from, to) = (to, from);
            if (from.Line == to.Line) return lines[from.Line].Substring(from.Col, to.Col - from.Col);
            var sb = new StringBuilder();
            sb.Append(lines[from.Line], from.Col, lines[from.Line].Length - from.Col);
            for (int l = from.Line + 1; l < to.Line; l++) sb.Append('\n').Append(lines[l]);
            sb.Append('\n').Append(lines[to.Line], 0, to.Col);
            return sb.ToString();
        }

        public string SelectedText => HasSelection ? GetText(SelStart, SelEnd) : "";

        /// <summary>The position of an offset in <see cref="Text"/>.</summary>
        public TextPos PosOf(int offset)
        {
            for (int l = 0; l < lines.Count; l++)
            {
                if (offset <= lines[l].Length) return new TextPos(l, Math.Max(0, offset));
                offset -= lines[l].Length + 1;
            }
            return End;
        }

        /// <summary>Offset of a position in <see cref="Text"/> (lines joined with '\n').</summary>
        public int OffsetOf(TextPos p)
        {
            int o = 0;
            for (int l = 0; l < p.Line && l < lines.Count; l++) o += lines[l].Length + 1;
            return o + p.Col;
        }

        // ---- the one primitive change, and undo ----

        sealed class Edit
        {
            public TextPos From;
            public string Removed, Inserted;
            public TextPos CaretBefore, AnchorBefore, CaretAfter, AnchorAfter;
        }
        sealed class Step { public readonly List<Edit> Edits = new List<Edit>(); public string Kind; public double Time; }
        readonly List<Step> undo = new List<Step>();
        readonly List<Step> redo = new List<Step>();
        Step open; // the step typing keeps adding to
        const int UndoLimit = 1000;

        /// <summary>Replaces the text between two positions; returns the end of the inserted text.</summary>
        TextPos ReplaceRaw(TextPos from, TextPos to, string text)
        {
            if (to < from) (from, to) = (to, from);
            var head = lines[from.Line].Substring(0, from.Col);
            var tail = lines[to.Line].Substring(to.Col);
            var parts = Split(text);
            lines.RemoveRange(from.Line, to.Line - from.Line + 1);
            var fresh = new string[parts.Length];
            for (int i = 0; i < parts.Length; i++) fresh[i] = parts[i];
            fresh[0] = head + fresh[0];
            int endCol = fresh[parts.Length - 1].Length;
            fresh[parts.Length - 1] += tail;
            lines.InsertRange(from.Line, fresh);
            Changed(from.Line);
            return new TextPos(from.Line + parts.Length - 1, endCol);
        }

        /// <summary>
        /// Replaces [from, to) with text, recorded for undo. Edits of the same kind close together in time and place
        /// (typing a word, deleting with backspace) join one undo step.
        /// </summary>
        public TextPos Replace(TextPos from, TextPos to, string text, string kind = null)
        {
            from = Clamp(from); to = Clamp(to);
            if (to < from) (from, to) = (to, from);
            text = text.Replace("\r\n", "\n").Replace('\r', '\n');
            var edit = new Edit { From = from, Removed = GetText(from, to), Inserted = text, CaretBefore = Caret, AnchorBefore = Anchor };
            var end = ReplaceRaw(from, to, text);
            // A high surrogate typed before a lone low one makes a pair on the spot: the caret goes after it.
            var endLine = lines[end.Line];
            if (end.Col > 0 && end.Col < endLine.Length && char.IsLowSurrogate(endLine[end.Col]) && char.IsHighSurrogate(endLine[end.Col - 1])) end.Col++;
            Caret = Anchor = end;
            wantedCol = -1;
            edit.CaretAfter = edit.AnchorAfter = end;

            if (group != null)
            {
                if (group.Edits.Count == 0) { undo.Add(group); if (undo.Count > UndoLimit) undo.RemoveAt(0); }
                group.Edits.Add(edit);
                redo.Clear();
                return end;
            }
            double now = Clock();
            var last = open != null && undo.Count > 0 && undo[undo.Count - 1] == open ? open : null;
            bool join = kind != null && last != null && last.Kind == kind && now - last.Time < 1.0 && Continues(last.Edits[last.Edits.Count - 1], edit, kind)
                        && !(kind == "type" && text != " " && last.Edits[last.Edits.Count - 1].Inserted.EndsWith(" ")); // a word after a space starts a new step
            if (join) { last.Edits.Add(edit); last.Time = now; }
            else
            {
                var step = new Step { Kind = kind, Time = now };
                step.Edits.Add(edit);
                undo.Add(step);
                if (undo.Count > UndoLimit) undo.RemoveAt(0);
                open = kind != null ? step : null;
            }
            redo.Clear();
            return end;
        }

        static bool Continues(Edit prev, Edit next, string kind)
        {
            if (kind == "type") return next.From == prev.CaretAfter && next.Removed.Length == 0 && !next.Inserted.Contains("\n");
            if (kind == "backspace") return next.Inserted.Length == 0 && next.From < prev.From && Advance(next.From, next.Removed) == prev.From && !next.Removed.Contains("\n");
            if (kind == "delete") return next.Inserted.Length == 0 && next.From == prev.From && !next.Removed.Contains("\n");
            return false;
        }

        static TextPos Advance(TextPos p, string text)
        {
            int nl = text.LastIndexOf('\n');
            if (nl < 0) return new TextPos(p.Line, p.Col + text.Length);
            int count = 0; foreach (var c in text) if (c == '\n') count++;
            return new TextPos(p.Line + count, text.Length - nl - 1);
        }

        Step group;
        /// <summary>Makes the edits until <see cref="EndGroup"/> one undo step (indenting a block, commenting lines).</summary>
        public void BeginGroup() { Seal(); group = new Step(); }
        public void EndGroup() { group = null; Seal(); }

        /// <summary>Ends the current undo step (a caret move or a click does, as in other editors).</summary>
        public void Seal() => open = null;

        public bool CanUndo => undo.Count > 0;
        public bool CanRedo => redo.Count > 0;

        public void Undo()
        {
            if (undo.Count == 0) return;
            var step = undo[undo.Count - 1];
            undo.RemoveAt(undo.Count - 1);
            for (int i = step.Edits.Count - 1; i >= 0; i--)
            {
                var e = step.Edits[i];
                ReplaceRaw(e.From, Advance(e.From, e.Inserted), e.Removed);
            }
            Caret = step.Edits[0].CaretBefore; Anchor = step.Edits[0].AnchorBefore;
            redo.Add(step);
            open = null; wantedCol = -1;
        }

        public void Redo()
        {
            if (redo.Count == 0) return;
            var step = redo[redo.Count - 1];
            redo.RemoveAt(redo.Count - 1);
            foreach (var e in step.Edits) ReplaceRaw(e.From, Advance(e.From, e.Removed), e.Inserted);
            var lastEdit = step.Edits[step.Edits.Count - 1];
            Caret = lastEdit.CaretAfter; Anchor = lastEdit.AnchorAfter;
            undo.Add(step);
            open = null; wantedCol = -1;
        }

        // ---- editing commands ----

        /// <summary>Types text over the selection (one undo step per typed word).</summary>
        public void Type(string text)
        {
            if (text.Length == 0) return;
            bool single = text.Length <= 2 && !text.Contains("\n") && !HasSelection;
            Replace(SelStart, SelEnd, text, single ? "type" : null);
            if (!single) Seal();
        }

        /// <summary>Pastes: like typing, but always its own undo step.</summary>
        public void Paste(string text) { Seal(); Replace(SelStart, SelEnd, text ?? ""); Seal(); }

        public void Backspace(bool word = false)
        {
            if (HasSelection) { Seal(); Replace(SelStart, SelEnd, ""); Seal(); return; }
            if (Caret.Line == 0 && Caret.Col == 0) return;
            TextPos from;
            if (word) from = WordLeft(Caret);
            else if (Caret.Col == 0) from = new TextPos(Caret.Line - 1, lines[Caret.Line - 1].Length);
            else
            {
                var line = lines[Caret.Line];
                // In leading spaces, step back to the previous tab stop.
                int lead = 0; while (lead < line.Length && line[lead] == ' ') lead++;
                if (Caret.Col <= lead && Caret.Col > 0 && line.Substring(0, Caret.Col).Trim().Length == 0)
                    from = new TextPos(Caret.Line, ((Caret.Col - 1) / TabSize) * TabSize);
                else from = new TextPos(Caret.Line, Caret.Col - CharLeftSize(line, Caret.Col));
            }
            Replace(from, Caret, "", word ? null : "backspace");
        }

        public void Delete(bool word = false)
        {
            if (HasSelection) { Seal(); Replace(SelStart, SelEnd, ""); Seal(); return; }
            if (Caret == End) return;
            TextPos to;
            if (word) to = WordRight(Caret);
            else if (Caret.Col == lines[Caret.Line].Length) to = new TextPos(Caret.Line + 1, 0);
            else to = new TextPos(Caret.Line, Caret.Col + CharRightSize(lines[Caret.Line], Caret.Col));
            var keep = Caret;
            Replace(Caret, to, "", word ? null : "delete");
            Caret = Anchor = keep;
        }

        /// <summary>Enter: keeps the indent, adds one after '{', and opens a block when the caret sits between "{}".</summary>
        public void NewLine()
        {
            Seal();
            var line = lines[SelStart.Line];
            int lead = 0; while (lead < line.Length && (line[lead] == ' ' || line[lead] == '\t')) lead++;
            var indent = line.Substring(0, Math.Min(lead, SelStart.Col));
            var before = line.Substring(0, SelStart.Col).TrimEnd();
            var after = lines[SelEnd.Line].Substring(SelEnd.Col).TrimStart();
            bool opens = before.EndsWith("{");
            if (opens && after.StartsWith("}"))
            {
                var start = SelStart;
                Replace(SelStart, SelEnd, "\n" + indent + new string(' ', TabSize) + "\n" + indent);
                Caret = Anchor = new TextPos(start.Line + 1, indent.Length + TabSize);
            }
            else Replace(SelStart, SelEnd, "\n" + indent + (opens ? new string(' ', TabSize) : ""));
            Seal();
        }

        /// <summary>Tab / Shift+Tab: over several lines, indents or outdents them; otherwise pads to the next tab stop.</summary>
        public void Indent(bool outdent)
        {
            Seal();
            if (!outdent && (!HasSelection || SelStart.Line == SelEnd.Line))
            {
                int col = SelStart.Col;
                Replace(SelStart, SelEnd, new string(' ', TabSize - col % TabSize));
                Seal();
                return;
            }
            var (first, last) = SelectedLines();
            var caret = Caret; var anchor = Anchor;
            var step = new List<(int line, int delta)>();
            BeginGroup();
            for (int l = first; l <= last; l++)
            {
                var line = lines[l];
                if (!outdent)
                {
                    if (line.Length == 0) continue;
                    Replace(new TextPos(l, 0), new TextPos(l, 0), new string(' ', TabSize), "indent");
                    step.Add((l, TabSize));
                }
                else
                {
                    int n = 0; while (n < TabSize && n < line.Length && line[n] == ' ') n++;
                    if (n == 0 && line.Length > 0 && line[0] == '\t') n = 1;
                    if (n == 0) continue;
                    Replace(new TextPos(l, 0), new TextPos(l, n), "", "indent");
                    step.Add((l, -n));
                }
            }
            EndGroup();
            int Shift(TextPos p) { foreach (var (line, delta) in step) if (line == p.Line) return Math.Max(0, p.Col + delta); return p.Col; }
            Caret = Clamp(new TextPos(caret.Line, Shift(caret)));
            Anchor = Clamp(new TextPos(anchor.Line, Shift(anchor)));
            Seal();
        }

        /// <summary>Ctrl+/ : comments the lines out with "// ", or back in when all are commented.</summary>
        public void ToggleComment()
        {
            Seal();
            var (first, last) = SelectedLines();
            bool allCommented = true;
            int minLead = int.MaxValue;
            for (int l = first; l <= last; l++)
            {
                var t = lines[l];
                if (t.Trim().Length == 0) continue;
                int lead = t.Length - t.TrimStart().Length;
                minLead = Math.Min(minLead, lead);
                if (!t.TrimStart().StartsWith("//")) allCommented = false;
            }
            if (minLead == int.MaxValue) return;
            var caret = Caret; var anchor = Anchor;
            BeginGroup();
            for (int l = first; l <= last; l++)
            {
                var t = lines[l];
                if (t.Trim().Length == 0) continue;
                if (allCommented)
                {
                    int at = t.IndexOf("//", StringComparison.Ordinal);
                    int n = t.Length > at + 2 && t[at + 2] == ' ' ? 3 : 2;
                    Replace(new TextPos(l, at), new TextPos(l, at + n), "", "comment");
                }
                else Replace(new TextPos(l, minLead), new TextPos(l, minLead), "// ", "comment");
            }
            EndGroup();
            int d = allCommented ? -3 : 3;
            Caret = Clamp(new TextPos(caret.Line, Math.Max(0, caret.Col + d)));
            Anchor = Clamp(new TextPos(anchor.Line, Math.Max(0, anchor.Col + d)));
            Seal();
        }

        /// <summary>The lines a selection covers (one ending at column 0 leaves that line out).</summary>
        (int first, int last) SelectedLines() => (SelStart.Line, HasSelection && SelEnd.Col == 0 && SelEnd.Line > SelStart.Line ? SelEnd.Line - 1 : SelEnd.Line);

        /// <summary>Ctrl+D: duplicates the line (or the selected lines) below.</summary>
        public void DuplicateLines()
        {
            Seal();
            int first = SelStart.Line, last = SelEnd.Line;
            var block = GetText(new TextPos(first, 0), new TextPos(last, lines[last].Length));
            var caret = Caret; var anchor = Anchor;
            Replace(new TextPos(last, lines[last].Length), new TextPos(last, lines[last].Length), "\n" + block);
            int shift = last - first + 1;
            Caret = new TextPos(caret.Line + shift, caret.Col); Anchor = new TextPos(anchor.Line + shift, anchor.Col);
            Seal();
        }

        // ---- caret moves ----

        static int CharLeftSize(string line, int col) => col >= 2 && char.IsLowSurrogate(line[col - 1]) && char.IsHighSurrogate(line[col - 2]) ? 2 : 1;
        static int CharRightSize(string line, int col) => col + 1 < line.Length && char.IsHighSurrogate(line[col]) && char.IsLowSurrogate(line[col + 1]) ? 2 : 1;

        static int Kind(char c) => char.IsWhiteSpace(c) ? 0 : char.IsLetterOrDigit(c) || c == '_' ? 1 : 2;

        public TextPos WordLeft(TextPos p)
        {
            if (p.Col == 0) return p.Line == 0 ? p : new TextPos(p.Line - 1, lines[p.Line - 1].Length);
            var line = lines[p.Line];
            int i = p.Col;
            while (i > 0 && Kind(line[i - 1]) == 0) i--;
            if (i == 0) return new TextPos(p.Line, 0);
            int k = Kind(line[i - 1]);
            while (i > 0 && Kind(line[i - 1]) == k) i--;
            return new TextPos(p.Line, i);
        }

        public TextPos WordRight(TextPos p)
        {
            var line = lines[p.Line];
            if (p.Col >= line.Length) return p.Line + 1 < lines.Count ? new TextPos(p.Line + 1, 0) : p;
            int i = p.Col;
            int k = Kind(line[i]);
            while (i < line.Length && Kind(line[i]) == k) i++;
            while (i < line.Length && Kind(line[i]) == 0) i++;
            return new TextPos(p.Line, i);
        }

        /// <summary>Moves the caret; with select, extends the selection instead of dropping it.</summary>
        public void MoveTo(TextPos p, bool select, bool keepWantedCol = false)
        {
            Seal();
            Caret = Clamp(p);
            if (!select) Anchor = Caret;
            if (!keepWantedCol) wantedCol = -1;
        }

        public void Left(bool select, bool word)
        {
            if (HasSelection && !select && !word) { MoveTo(SelStart, false); return; }
            var p = Caret;
            if (word) p = WordLeft(p);
            else if (p.Col > 0) p.Col -= CharLeftSize(lines[p.Line], p.Col);
            else if (p.Line > 0) p = new TextPos(p.Line - 1, lines[p.Line - 1].Length);
            MoveTo(p, select);
        }

        public void Right(bool select, bool word)
        {
            if (HasSelection && !select && !word) { MoveTo(SelEnd, false); return; }
            var p = Caret;
            if (word) p = WordRight(p);
            else if (p.Col < lines[p.Line].Length) p.Col += CharRightSize(lines[p.Line], p.Col);
            else if (p.Line + 1 < lines.Count) p = new TextPos(p.Line + 1, 0);
            MoveTo(p, select);
        }

        /// <summary>Up/down by n lines (page moves pass the page height); the caret keeps its wanted column.</summary>
        public void Vertical(int delta, bool select)
        {
            if (wantedCol < 0) wantedCol = Caret.Col;
            int line = Math.Max(0, Math.Min(lines.Count - 1, Caret.Line + delta));
            var target = line == Caret.Line && delta < 0 ? new TextPos(0, 0) : line == Caret.Line && delta > 0 ? End : new TextPos(line, wantedCol);
            int keep = wantedCol;
            MoveTo(target, select, true);
            wantedCol = keep;
        }

        /// <summary>Home: to the first non-blank character, or to column 0 when already there.</summary>
        public void Home(bool select)
        {
            var line = lines[Caret.Line];
            int lead = 0; while (lead < line.Length && char.IsWhiteSpace(line[lead])) lead++;
            MoveTo(new TextPos(Caret.Line, Caret.Col == lead ? 0 : lead), select);
        }

        public void EndKey(bool select) => MoveTo(new TextPos(Caret.Line, lines[Caret.Line].Length), select);
        public void DocStart(bool select) => MoveTo(new TextPos(0, 0), select);
        public void DocEnd(bool select) => MoveTo(End, select);
        public void SelectAll() { Seal(); Anchor = new TextPos(0, 0); Caret = End; }

        public void SelectWord(TextPos p)
        {
            p = Clamp(p);
            var line = lines[p.Line];
            if (line.Length == 0) { MoveTo(p, false); return; }
            int i = Math.Min(p.Col, line.Length - 1);
            int k = Kind(line[i]);
            int a = i, b = i;
            while (a > 0 && Kind(line[a - 1]) == k) a--;
            while (b < line.Length && Kind(line[b]) == k) b++;
            Seal();
            Anchor = new TextPos(p.Line, a); Caret = new TextPos(p.Line, b);
        }

        public void SelectLine(int line)
        {
            line = Math.Max(0, Math.Min(lines.Count - 1, line));
            Seal();
            Anchor = new TextPos(line, 0);
            Caret = line + 1 < lines.Count ? new TextPos(line + 1, 0) : new TextPos(line, lines[line].Length);
        }
    }
}
