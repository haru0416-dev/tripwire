using System;
using UnityEngine;

namespace UdonIde.Editing
{
    /// <summary>What a key press did: nothing, moved the caret, or changed the text (and maybe asked to save).</summary>
    public enum KeyResult { Ignored, Moved, Edited, Save }

    /// <summary>
    /// Keys to buffer commands, away from the GUI so tests can press keys without a window. Ctrl stands for Command on
    /// macOS (the view passes either).
    /// </summary>
    public static class KeyMap
    {
        public static KeyResult Apply(TextBuffer b, KeyCode key, char ch, bool ctrl, bool shift, bool alt, int pageLines,
            Func<string> getClipboard, Action<string> setClipboard)
        {
            if (ctrl)
            {
                switch (key)
                {
                    case KeyCode.A: b.SelectAll(); return KeyResult.Moved;
                    case KeyCode.C: if (b.HasSelection) setClipboard(b.SelectedText); else setClipboard(b.Lines[b.Caret.Line] + "\n"); return KeyResult.Ignored;
                    case KeyCode.X:
                        if (!b.HasSelection) b.SelectLine(b.Caret.Line);
                        setClipboard(b.SelectedText); b.Backspace(); return KeyResult.Edited;
                    case KeyCode.V: b.Paste(getClipboard()); return KeyResult.Edited;
                    case KeyCode.Z: if (shift) b.Redo(); else b.Undo(); return KeyResult.Edited;
                    case KeyCode.Y: b.Redo(); return KeyResult.Edited;
                    case KeyCode.D: b.DuplicateLines(); return KeyResult.Edited;
                    case KeyCode.Slash: b.ToggleComment(); return KeyResult.Edited;
                    case KeyCode.S: return KeyResult.Save;
                    case KeyCode.LeftArrow: b.Left(shift, true); return KeyResult.Moved;
                    case KeyCode.RightArrow: b.Right(shift, true); return KeyResult.Moved;
                    case KeyCode.Home: b.DocStart(shift); return KeyResult.Moved;
                    case KeyCode.End: b.DocEnd(shift); return KeyResult.Moved;
                    case KeyCode.Backspace: b.Backspace(true); return KeyResult.Edited;
                    case KeyCode.Delete: b.Delete(true); return KeyResult.Edited;
                    default: return KeyResult.Ignored;
                }
            }
            switch (key)
            {
                case KeyCode.LeftArrow: b.Left(shift, alt); return KeyResult.Moved;
                case KeyCode.RightArrow: b.Right(shift, alt); return KeyResult.Moved;
                case KeyCode.UpArrow: b.Vertical(-1, shift); return KeyResult.Moved;
                case KeyCode.DownArrow: b.Vertical(1, shift); return KeyResult.Moved;
                case KeyCode.PageUp: b.Vertical(-pageLines, shift); return KeyResult.Moved;
                case KeyCode.PageDown: b.Vertical(pageLines, shift); return KeyResult.Moved;
                case KeyCode.Home: b.Home(shift); return KeyResult.Moved;
                case KeyCode.End: b.EndKey(shift); return KeyResult.Moved;
                case KeyCode.Backspace: b.Backspace(); return KeyResult.Edited;
                case KeyCode.Delete: b.Delete(); return KeyResult.Edited;
                case KeyCode.Return: case KeyCode.KeypadEnter: b.NewLine(); return KeyResult.Edited;
                case KeyCode.Tab: b.Indent(shift); return KeyResult.Edited;
                case KeyCode.Escape: if (b.HasSelection) { b.MoveTo(b.Caret, false); return KeyResult.Moved; } return KeyResult.Ignored;
            }
            // Typed characters (IMGUI sends them as a separate event with KeyCode.None; IME commits come the same way).
            if (ch != 0 && !char.IsControl(ch))
            {
                TypeChar(b, ch);
                return KeyResult.Edited;
            }
            return KeyResult.Ignored;
        }

        /// <summary>Whether a key would change the text (read-only files ignore those).</summary>
        public static bool IsEdit(KeyCode key, char ch, bool ctrl)
        {
            if (ctrl) return key == KeyCode.X || key == KeyCode.V || key == KeyCode.Z || key == KeyCode.Y || key == KeyCode.D || key == KeyCode.Slash
                             || key == KeyCode.Backspace || key == KeyCode.Delete;
            if (key == KeyCode.Backspace || key == KeyCode.Delete || key == KeyCode.Return || key == KeyCode.KeypadEnter || key == KeyCode.Tab) return true;
            return ch != 0 && !char.IsControl(ch);
        }

        /// <summary>Types a character; "}" on a blank indented line outdents the line first.</summary>
        public static void TypeChar(TextBuffer b, char ch)
        {
            var line = b.Lines[b.Caret.Line];
            if (ch == '}' && !b.HasSelection && b.Caret.Col == line.Length && line.Length > 0 && line.Trim().Length == 0)
            {
                // One indent step back: a tab, or up to one tab stop of spaces.
                int cut = line.EndsWith("\t") ? 1 : System.Math.Min(TextBuffer.TabSize, line.Length - line.TrimEnd(' ').Length);
                b.Replace(new TextPos(b.Caret.Line, 0), new TextPos(b.Caret.Line, line.Length), line.Substring(0, line.Length - cut) + "}", "type");
                return;
            }
            b.Type(ch.ToString());
        }
    }
}
