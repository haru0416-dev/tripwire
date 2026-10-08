using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UdonIde.Editing;
using UnityEngine;

namespace UdonIde.Tests
{
    public class EditingTests
    {
        static TextBuffer Buf(string text, int line = 0, int col = 0)
        {
            double t = 0;
            var b = new TextBuffer { Clock = () => t += 0.1 };
            b.Text = text;
            b.Caret = b.Anchor = new TextPos(line, col);
            return b;
        }

        static KeyResult Key(TextBuffer b, KeyCode k, bool ctrl = false, bool shift = false, string clip = "") =>
            KeyMap.Apply(b, k, '\0', ctrl, shift, false, 10, () => clip, _ => { });

        static void TypeAll(TextBuffer b, string s) { foreach (var c in s) KeyMap.TypeChar(b, c); }

        [Test]
        public void TypingAndUndoGroupsWords()
        {
            var b = Buf("");
            TypeAll(b, "int x = 1;");
            Assert.AreEqual("int x = 1;", b.Text);
            b.Undo(); // the last word, "1;"
            Assert.AreEqual("int x = ", b.Text);
            while (b.CanUndo) b.Undo();
            Assert.AreEqual("", b.Text);
            while (b.CanRedo) b.Redo();
            Assert.AreEqual("int x = 1;", b.Text);
        }

        [Test]
        public void TypingPauseStartsNewUndoStep()
        {
            double t = 0;
            var b = new TextBuffer { Clock = () => t };
            b.Text = "";
            KeyMap.TypeChar(b, 'a'); t += 0.2; KeyMap.TypeChar(b, 'b'); t += 2.0; KeyMap.TypeChar(b, 'c');
            b.Undo();
            Assert.AreEqual("ab", b.Text);
        }

        [Test]
        public void EnterKeepsIndentAndOpensBlocks()
        {
            var b = Buf("    void F() {}", 0, 14);
            Key(b, KeyCode.Return);
            Assert.AreEqual("    void F() {\n        \n    }", b.Text);
            Assert.AreEqual(new TextPos(1, 8), b.Caret);
            TypeAll(b, "x();");
            Key(b, KeyCode.Return);
            Assert.AreEqual("        ", b.Lines[2]);
            KeyMap.TypeChar(b, '}'); // outdents on a blank line
            Assert.AreEqual("    }", b.Lines[2]);
        }

        [Test]
        public void ClosingBraceOutdentsTabsByOneStep()
        {
            var b = Buf("{\n\t\t", 1, 2);
            KeyMap.TypeChar(b, '}');
            Assert.AreEqual("\t}", b.Lines[1], "one tab back, not all of them");
            var c = Buf("{\n      ", 1, 6);
            KeyMap.TypeChar(c, '}');
            Assert.AreEqual("  }", c.Lines[1], "up to one tab stop of spaces");
        }

        [Test]
        public void BackspaceInIndentGoesToTabStop()
        {
            var b = Buf("        x", 0, 8);
            Key(b, KeyCode.Backspace);
            Assert.AreEqual("    x", b.Text);
            var c = Buf("      x", 0, 6);
            Key(c, KeyCode.Backspace);
            Assert.AreEqual("    x", c.Text);
        }

        [Test]
        public void BackspaceJoinsLinesAndUndoesAsOne()
        {
            var b = Buf("ab\ncd", 1, 0);
            Key(b, KeyCode.Backspace);
            Assert.AreEqual("abcd", b.Text);
            Assert.AreEqual(new TextPos(0, 2), b.Caret);
            b.Undo();
            Assert.AreEqual("ab\ncd", b.Text);
            Assert.AreEqual(new TextPos(1, 0), b.Caret);
        }

        [Test]
        public void TabIndentsSelectedLinesAndShiftTabOutdents()
        {
            var b = Buf("a\nb\n\nc");
            b.Anchor = new TextPos(0, 0); b.Caret = new TextPos(3, 1);
            Key(b, KeyCode.Tab);
            Assert.AreEqual("    a\n    b\n\n    c", b.Text);
            Assert.AreEqual(new TextPos(3, 5), b.Caret);
            Key(b, KeyCode.Tab, shift: true);
            Assert.AreEqual("a\nb\n\nc", b.Text);
            Key(b, KeyCode.Tab);
            b.Undo(); // one step for the whole block
            Assert.AreEqual("a\nb\n\nc", b.Text);
        }

        [Test]
        public void TabWithoutSelectionPadsToTabStop()
        {
            var b = Buf("ab", 0, 2);
            Key(b, KeyCode.Tab);
            Assert.AreEqual("ab  ", b.Text);
        }

        [Test]
        public void ToggleCommentBothWays()
        {
            var b = Buf("    a();\n      b();");
            b.Anchor = new TextPos(0, 0); b.Caret = new TextPos(1, 3);
            Key(b, KeyCode.Slash, ctrl: true);
            Assert.AreEqual("    // a();\n    //   b();", b.Text);
            Key(b, KeyCode.Slash, ctrl: true);
            Assert.AreEqual("    a();\n      b();", b.Text);
        }

        [Test]
        public void CutCopyPasteWholeLineWithoutSelection()
        {
            var b = Buf("one\ntwo\nthree", 1, 1);
            string clip = null;
            KeyMap.Apply(b, KeyCode.X, '\0', true, false, false, 10, () => clip, s => clip = s);
            Assert.AreEqual("one\nthree", b.Text);
            Assert.AreEqual("two\n", clip);
            b.MoveTo(new TextPos(1, 0), false);
            KeyMap.Apply(b, KeyCode.V, '\0', true, false, false, 10, () => clip, s => clip = s);
            Assert.AreEqual("one\ntwo\nthree", b.Text);
        }

        [Test]
        public void PasteNormalizesLineEndings()
        {
            var b = Buf("", 0, 0);
            b.Paste("a\r\nb\rc");
            Assert.AreEqual("a\nb\nc", b.Text);
            Assert.AreEqual(3, b.LineCount);
        }

        [Test]
        public void SurrogatePairsAreNeverSplit()
        {
            var b = Buf("a😀b", 0, 1);
            Key(b, KeyCode.RightArrow);
            Assert.AreEqual(new TextPos(0, 3), b.Caret, "steps over the pair");
            Key(b, KeyCode.Backspace);
            Assert.AreEqual("ab", b.Text, "removes both halves");
            var c = Buf("a😀b", 0, 1);
            Key(c, KeyCode.Delete);
            Assert.AreEqual("ab", c.Text);
            Assert.AreEqual(new TextPos(0, 1), Buf("a😀b").Clamp(new TextPos(0, 2)), "a position inside a pair snaps to its start");
            var d = Buf("x\uDE00", 0, 1);
            KeyMap.TypeChar(d, '\uD83D'); // the two halves of an emoji, typed in two events
            Assert.AreEqual(new TextPos(0, 3), d.Caret, "a pair made by typing: the caret goes after it");
        }

        [Test]
        public void WordMovesOverJapaneseAndSymbols()
        {
            var b = Buf("int 開いた回数 = a.b;", 0, 0);
            Key(b, KeyCode.RightArrow, ctrl: true);
            Assert.AreEqual(4, b.Caret.Col);
            Key(b, KeyCode.RightArrow, ctrl: true);
            Assert.AreEqual(10, b.Caret.Col);
            Key(b, KeyCode.LeftArrow, ctrl: true);
            Assert.AreEqual(4, b.Caret.Col);
            Key(b, KeyCode.Backspace, ctrl: true);
            Assert.AreEqual("開いた回数 = a.b;", b.Text);
        }

        [Test]
        public void UpDownKeepsWantedColumn()
        {
            var b = Buf("long line here\nab\nanother long line", 0, 10);
            Key(b, KeyCode.DownArrow);
            Assert.AreEqual(new TextPos(1, 2), b.Caret);
            Key(b, KeyCode.DownArrow);
            Assert.AreEqual(new TextPos(2, 10), b.Caret);
            Key(b, KeyCode.DownArrow);
            Assert.AreEqual(new TextPos(2, 17), b.Caret, "past the last line: to the end");
        }

        [Test]
        public void HomeTogglesBetweenIndentAndColumnZero()
        {
            var b = Buf("    x", 0, 5);
            Key(b, KeyCode.Home);
            Assert.AreEqual(4, b.Caret.Col);
            Key(b, KeyCode.Home);
            Assert.AreEqual(0, b.Caret.Col);
        }

        [Test]
        public void TypingReplacesSelection()
        {
            var b = Buf("hello world");
            b.Anchor = new TextPos(0, 0); b.Caret = new TextPos(0, 5);
            KeyMap.TypeChar(b, 'X');
            Assert.AreEqual("X world", b.Text);
            b.Undo();
            Assert.AreEqual("hello world", b.Text);
            Assert.AreEqual(new TextPos(0, 5), b.Caret);
            Assert.AreEqual(new TextPos(0, 0), b.Anchor, "selection comes back with undo");
        }

        [Test]
        public void DuplicateLines()
        {
            var b = Buf("a\nb", 0, 1);
            Key(b, KeyCode.D, ctrl: true);
            Assert.AreEqual("a\na\nb", b.Text);
            Assert.AreEqual(new TextPos(1, 1), b.Caret);
        }

        [Test]
        public void SelectWordAndLine()
        {
            var b = Buf("foo.barBaz(1)");
            b.SelectWord(new TextPos(0, 6));
            Assert.AreEqual("barBaz", b.SelectedText);
            b.SelectLine(0);
            Assert.AreEqual("foo.barBaz(1)", b.SelectedText);
        }

        [Test]
        public void EditsAtTheEdges()
        {
            var b = Buf("", 0, 0);
            Key(b, KeyCode.Backspace); Key(b, KeyCode.Delete); Key(b, KeyCode.LeftArrow); Key(b, KeyCode.UpArrow);
            Assert.AreEqual("", b.Text);
            Assert.AreEqual(new TextPos(0, 0), b.Caret);
            b.Undo(); b.Redo();
            Assert.AreEqual("", b.Text);
        }

        // ---- lexer ----

        static List<Token> Lex(string line, LexState state, out LexState end)
        {
            var t = new List<Token>();
            end = LineLexer.Lex(line, state, t);
            return t;
        }

        static string Kinds(string line, List<Token> tokens) =>
            string.Join(" ", tokens.Where(t => t.Kind != TokenKind.Plain).Select(t => t.Kind + ":" + line.Substring(t.Start, t.Length)));

        [Test]
        public void LexerColorsALine()
        {
            var line = "public int Count = 3; // n";
            var t = Lex(line, LexState.Normal, out var end);
            Assert.AreEqual("Keyword:public Type:int Type:Count Number:3 Comment:// n", Kinds(line, t));
            Assert.AreEqual(LexState.Normal, end);
            Assert.AreEqual(line.Length, t.Sum(x => x.Length) + CountUncovered(line, t), "tokens cover the line");
        }

        static int CountUncovered(string line, List<Token> t)
        {
            var covered = new bool[line.Length];
            foreach (var x in t) for (int i = x.Start; i < x.Start + x.Length; i++) { Assert.IsFalse(covered[i], "tokens overlap"); covered[i] = true; }
            return covered.Count(c => !c);
        }

        [Test]
        public void BlockCommentsAndVerbatimStringsRunAcrossLines()
        {
            Lex("int a; /* start", LexState.Normal, out var s1);
            Assert.AreEqual(LexState.BlockComment, s1);
            var t = Lex("still */ int b;", s1, out var s2);
            Assert.AreEqual("Comment:still */ Type:int", Kinds("still */ int b;", t));
            Assert.AreEqual(LexState.Normal, s2);

            Lex("var s = @\"one \"\" quote", LexState.Normal, out var v1);
            Assert.AreEqual(LexState.VerbatimString, v1);
            var t2 = Lex("two\"; int c;", v1, out var v2);
            Assert.AreEqual("String:two\" Type:int", Kinds("two\"; int c;", t2));
            Assert.AreEqual(LexState.Normal, v2);
        }

        [Test]
        public void TagsInCodeStayText()
        {
            var line = "Debug.Log(\"<b>bold</b>\" + x<y);";
            var t = Lex(line, LexState.Normal, out _);
            Assert.AreEqual("Type:Debug Type:Log String:\"<b>bold</b>\"", Kinds(line, t));
        }

        [Test]
        public void LineStatesFollowEdits()
        {
            var b = Buf("a\nb\nc\nd");
            var st = new LineStates();
            Assert.AreEqual(LexState.Normal, st.StartOf(b, 3));
            b.MoveTo(new TextPos(0, 1), false);
            b.Type("/*");
            st.Invalidate(b.TakeChangedFrom());
            Assert.AreEqual(LexState.BlockComment, st.StartOf(b, 3), "a comment opened on line 1 reaches line 4");
            b.MoveTo(new TextPos(2, 1), false);
            b.Type("*/");
            st.Invalidate(b.TakeChangedFrom());
            Assert.AreEqual(LexState.BlockComment, st.StartOf(b, 2));
            Assert.AreEqual(LexState.Normal, st.StartOf(b, 3), "closed again on line 3");
        }

        // ---- fuzz: random edits keep the buffer sound, and undo/redo replay exactly ----

        [Test]
        public void RandomEditsUndoAndRedoExactly()
        {
            var rng = new System.Random(12345);
            var keys = new[] { KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.Home, KeyCode.End, KeyCode.Backspace,
                KeyCode.Delete, KeyCode.Return, KeyCode.Tab, KeyCode.PageUp, KeyCode.PageDown };
            const string chars = "ab {}();\"/*开😀\t x";
            for (int round = 0; round < 40; round++)
            {
                var start = "class A\n{\n    void F() { int 値 = 1; /* c */ }\n}\n";
                var b = Buf(start);
                string clip = "pasted\nlines";
                var history = new List<string> { b.Text };
                for (int i = 0; i < 300; i++)
                {
                    int op = rng.Next(10);
                    bool ctrl = rng.Next(6) == 0, shift = rng.Next(4) == 0;
                    if (op < 4) KeyMap.Apply(b, KeyCode.None, chars[rng.Next(chars.Length)], false, false, false, 5, () => clip, s => clip = s);
                    else if (op < 8) KeyMap.Apply(b, keys[rng.Next(keys.Length)], '\0', ctrl, shift, false, 5, () => clip, s => clip = s);
                    else if (op == 8) KeyMap.Apply(b, new[] { KeyCode.X, KeyCode.C, KeyCode.V, KeyCode.D, KeyCode.Slash, KeyCode.A }[rng.Next(6)], '\0', true, false, false, 5, () => clip, s => clip = s);
                    else b.MoveTo(new TextPos(rng.Next(b.LineCount + 2) - 1, rng.Next(40) - 2), rng.Next(2) == 0);
                    Assert.AreEqual(b.Clamp(b.Caret), b.Caret, $"caret valid (round {round}, op {i})");
                    Assert.AreEqual(b.Clamp(b.Anchor), b.Anchor, $"anchor valid (round {round}, op {i})");
                    Assert.AreEqual(b.Text, string.Join("\n", b.Lines), "lines and text agree");
                }
                var end = b.Text;
                while (b.CanUndo) b.Undo();
                Assert.AreEqual(start, b.Text, $"undo all returns to the start (round {round})");
                while (b.CanRedo) b.Redo();
                Assert.AreEqual(end, b.Text, $"redo all returns to the end (round {round})");
            }
        }
    }
}
