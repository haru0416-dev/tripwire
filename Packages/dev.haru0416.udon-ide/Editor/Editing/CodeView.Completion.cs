using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UdonIde.Editing
{
    public sealed partial class CodeView
    {
        UdonIde.CompletionList completion;
        readonly List<UdonIde.Completion> shown = new List<UdonIde.Completion>();
        int selected, firstShown;
        TextPos wordStart;
        UdonIde.Completion docFor;
        string docText;
        Rect popupRect;
        const int Rows = 10;

        internal bool CompletionOpen => completion != null;
        internal IReadOnlyList<UdonIde.Completion> CompletionItems => shown;

        public void OpenCompletion(bool asked)
        {
            if (ReadOnly || CompletionSource == null) return;
            var list = CompletionSource(Buffer.Caret);
            if (list == null || list.Items.Count == 0) { CloseCompletion(); return; }
            completion = list;
            wordStart = Buffer.PosOf(list.WordStart);
            selected = 0; firstShown = 0;
            Filter();
            if (shown.Count == 0 && !asked) CloseCompletion();
            MarkDirtyRepaint();
        }

        public void CloseCompletion() { completion = null; shown.Clear(); docFor = null; MarkDirtyRepaint(); }

        void Filter()
        {
            if (completion == null) return;
            if (Buffer.Caret.Line != wordStart.Line || Buffer.Caret.Col < wordStart.Col) { CloseCompletion(); return; }
            var typed = Buffer.GetText(wordStart, Buffer.Caret);
            var keep = selected < shown.Count ? shown[selected] : null;
            shown.Clear();
            // Words that start with what was typed, then words that contain it (case ignored either way).
            foreach (var i in completion.Items) if (i.Name.StartsWith(typed, StringComparison.OrdinalIgnoreCase)) shown.Add(i);
            if (typed.Length > 1) foreach (var i in completion.Items) if (!i.Name.StartsWith(typed, StringComparison.OrdinalIgnoreCase) && i.Name.IndexOf(typed, StringComparison.OrdinalIgnoreCase) >= 0) shown.Add(i);
            int exact = shown.FindIndex(i => i.Name == typed);
            selected = exact >= 0 ? exact : keep != null && shown.Contains(keep) && typed.Length == 0 ? shown.IndexOf(keep) : 0;
            firstShown = Mathf.Clamp(firstShown, 0, Math.Max(0, shown.Count - Rows));
            KeepSelectedShown();
            if (shown.Count == 0 && typed.Length > 0) CloseCompletion();
        }

        public void AcceptCompletion()
        {
            if (completion == null || selected >= shown.Count) { CloseCompletion(); return; }
            var item = shown[selected];
            Buffer.Seal();
            Buffer.Replace(wordStart, Buffer.Caret, item.Name);
            Buffer.Seal();
            CloseCompletion();
            Edited();
        }

        void Select(int index)
        {
            if (shown.Count == 0) return;
            selected = Mathf.Clamp(index, 0, shown.Count - 1);
            KeepSelectedShown();
            MarkDirtyRepaint();
        }

        void KeepSelectedShown() => firstShown = Mathf.Clamp(firstShown, selected - Rows + 1, selected);

        /// <summary>True when the event was the list's.</summary>
        bool HandleCompletionEvent(Event e, Rect all, Rect text)
        {
            if (e.type == EventType.MouseDown)
            {
                if (popupRect.Contains(e.mousePosition))
                {
                    int row = (int)((e.mousePosition.y - popupRect.y) / lineH);
                    if (row >= 0 && row < Math.Min(Rows, shown.Count)) { Select(firstShown + row); AcceptCompletion(); }
                    e.Use();
                    return true;
                }
                CloseCompletion();
                return false;
            }
            if (e.type == EventType.ScrollWheel && popupRect.Contains(e.mousePosition)) { Select(selected + (e.delta.y > 0 ? 1 : -1)); e.Use(); return true; }
            if (e.type != EventType.KeyDown) return false;
            switch (e.keyCode)
            {
                case KeyCode.DownArrow: Select(selected + 1); e.Use(); return true;
                case KeyCode.UpArrow: Select(selected - 1); e.Use(); return true;
                case KeyCode.PageDown: Select(selected + Rows); e.Use(); return true;
                case KeyCode.PageUp: Select(selected - Rows); e.Use(); return true;
                case KeyCode.Return: case KeyCode.KeypadEnter: case KeyCode.Tab: AcceptCompletion(); ScrollToCaret(text); e.Use(); return true;
                case KeyCode.Escape: CloseCompletion(); e.Use(); return true;
                case KeyCode.LeftArrow: case KeyCode.RightArrow: case KeyCode.Home: case KeyCode.End: CloseCompletion(); return false;
            }
            return false;
        }

        void AfterKey(Event e, KeyResult r, TextPos before)
        {
            char ch = e.character;
            if (r != KeyResult.Edited) { if (completion != null && e.keyCode != KeyCode.None) CloseCompletion(); return; }
            if (ch == '.') { OpenCompletion(false); return; }
            bool word = ch != 0 && (char.IsLetterOrDigit(ch) || ch == '_');
            if (completion != null)
            {
                if (word || e.keyCode == KeyCode.Backspace) Filter(); else CloseCompletion();
                return;
            }
            // The first letter of a word opens the list (one compile per word; typing on only filters it).
            if (word && !char.IsDigit(ch))
            {
                var line = Buffer.Lines[Buffer.Caret.Line];
                int at = Buffer.Caret.Col - 1;
                bool startsWord = at == 0 || !(char.IsLetterOrDigit(line[at - 1]) || line[at - 1] == '_');
                if (startsWord) OpenCompletion(false);
            }
        }

        static readonly Dictionary<UdonIde.CompletionKind, (string glyph, Color color)> KindGlyph = new Dictionary<UdonIde.CompletionKind, (string, Color)>
        {
            { UdonIde.CompletionKind.Method, ("m", new Color32(0xC5, 0x86, 0xC0, 0xFF)) }, { UdonIde.CompletionKind.Property, ("p", new Color32(0x9C, 0xDC, 0xFE, 0xFF)) },
            { UdonIde.CompletionKind.Field, ("f", new Color32(0x9C, 0xDC, 0xFE, 0xFF)) }, { UdonIde.CompletionKind.Local, ("l", new Color32(0x9C, 0xDC, 0xFE, 0xFF)) },
            { UdonIde.CompletionKind.Type, ("t", new Color32(0x4E, 0xC9, 0xB0, 0xFF)) }, { UdonIde.CompletionKind.EnumMember, ("e", new Color32(0xB8, 0xD7, 0xA3, 0xFF)) },
            { UdonIde.CompletionKind.Keyword, ("k", new Color32(0x56, 0x9C, 0xD6, 0xFF)) }, { UdonIde.CompletionKind.Namespace, ("n", new Color(0.7f, 0.7f, 0.7f)) },
            { UdonIde.CompletionKind.Event, ("v", new Color32(0xDC, 0xDC, 0xAA, 0xFF)) },
        };

        void DrawCompletion(Rect all, Vector2 caret)
        {
            if (shown.Count == 0) { popupRect = default; return; }
            float width = Math.Min(520, all.width - 16);
            int rows = Math.Min(Rows, shown.Count);
            float listH = rows * lineH + 4;
            var item = shown[selected];
            if (docFor != item) { docFor = item; docText = CompletionDoc?.Invoke(item) ?? item.Detail; }
            content.text = docText ?? "";
            float docH = string.IsNullOrEmpty(docText) ? 0 : Math.Min(Wrap.CalcHeight(content, width), lineH * 7 + 12);
            float x = Mathf.Clamp(caret.x - 24, all.x + 4, all.xMax - width - 4 - ScrollBar);
            float y = caret.y + lineH + 2;
            if (y + listH + docH > all.yMax - ScrollBar && caret.y - listH - docH - 2 > all.y) y = caret.y - listH - docH - 2;
            popupRect = new Rect(x, y, width, listH);
            EditorGUI.DrawRect(new Rect(x - 1, y - 1, width + 2, listH + docH + 2), PopupBorder);
            EditorGUI.DrawRect(popupRect, PopupBack);
            GUI.BeginClip(popupRect);
            for (int r = 0; r < rows; r++)
            {
                int i = firstShown + r;
                if (i >= shown.Count) break;
                var c = shown[i];
                float ry = 2 + r * lineH;
                if (i == selected) EditorGUI.DrawRect(new Rect(0, ry, width, lineH), PopupSelected);
                var (glyph, color) = KindGlyph.TryGetValue(c.Kind, out var g) ? g : ("?", Colors[0]);
                textStyle.normal.textColor = c.Exposed ? color : Dim.normal.textColor;
                content.text = glyph;
                textStyle.Draw(new Rect(8, ry + (lineH - textStyle.lineHeight) * 0.5f, 14, lineH), content, false, false, false, false);
                textStyle.normal.textColor = c.Exposed ? Colors[0] : Dim.normal.textColor;
                content.text = c.Name;
                var nameW = textStyle.CalcSize(content).x;
                textStyle.Draw(new Rect(26, ry + (lineH - textStyle.lineHeight) * 0.5f, nameW + 4, lineH), content, false, false, false, false);
                if (!string.IsNullOrEmpty(c.Detail))
                {
                    Dim.normal.textColor = new Color(0.5f, 0.5f, 0.53f);
                    content.text = c.Detail;
                    Dim.Draw(new Rect(26 + nameW + 16, ry + (lineH - textStyle.lineHeight) * 0.5f, width - nameW - 50, lineH), content, false, false, false, false);
                }
            }
            textStyle.normal.textColor = Colors[0];
            GUI.EndClip();
            if (docH > 0)
            {
                var docRect = new Rect(x, y + listH, width, docH);
                EditorGUI.DrawRect(docRect, PopupBack);
                EditorGUI.DrawRect(new Rect(x, y + listH, width, 1), PopupBorder);
                content.text = docText;
                Wrap.Draw(docRect, content, false, false, false, false);
            }
        }
    }
}
