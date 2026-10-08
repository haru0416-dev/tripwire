using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UdonIde.Editing
{
    public sealed partial class CodeView
    {
        string hover;
        Vector2 hoverMouse = new Vector2(-1000, -1000), hoverAt;
        double hoverSince;
        Rect textArea; // the text's rectangle in the last OnGUI (hover runs from a scheduler, outside OnGUI)

        void PollHover()
        {
            if (hover != null || completion != null || HoverSource == null || GUIUtility.hotControl != 0) return;
            if (EditorApplication.timeSinceStartup - hoverSince < 0.45 || !textArea.Contains(hoverMouse)) return;
            var p = HitTest(textArea, hoverMouse);
            // Only over text, not the blank space after a line's end.
            float endX = textArea.x + Pad - scroll.x + ColX(p.Line, Buffer.Lines[p.Line].Length);
            if (hoverMouse.x > endX + 2) { hoverSince = double.MaxValue; return; }
            hoverSince = double.MaxValue; // once per rest, even if the source fails
            hover = HoverText(p);
            if (hover == null) return;
            hoverAt = hoverMouse;
            MarkDirtyRepaint();
        }

        /// <summary>The problems under a place, then the source's text; null when neither has anything.</summary>
        string HoverText(TextPos p)
        {
            var parts = new List<string>();
            foreach (var pr in Problems)
                if (pr.File == null && pr.Line == p.Line && p.Col >= pr.Column && p.Col <= pr.Column + Math.Max(1, pr.Length))
                    parts.Add((pr.Error ? "エラー" : "注意") + $" [{pr.Source}] " + pr.Message);
            var info = HoverSource?.Invoke(p);
            if (!string.IsNullOrEmpty(info)) parts.Add(info);
            return parts.Count == 0 ? null : string.Join("\n\n", parts);
        }

        /// <summary>Dev: the hover for a place, now.</summary>
        internal string HoverAt(TextPos p)
        {
            hover = HoverText(p);
            if (lineH > 0) hoverAt = new Vector2(textArea.x + Pad - scroll.x + ColX(p.Line, p.Col), textArea.y + Pad - scroll.y + p.Line * lineH);
            MarkDirtyRepaint();
            return hover;
        }

        void DrawHover(Rect all)
        {
            float width = Math.Min(480, all.width - 16);
            content.text = hover;
            float h = Math.Min(Wrap.CalcHeight(content, width), all.height - 8);
            float x = Mathf.Clamp(hoverAt.x, all.x + 4, all.xMax - width - 4 - ScrollBar);
            float y = hoverAt.y + lineH + 2;
            if (y + h > all.yMax - ScrollBar) y = Math.Max(all.y + 4, hoverAt.y - h - 4);
            var r = new Rect(x, y, width, h);
            EditorGUI.DrawRect(new Rect(r.x - 1, r.y - 1, r.width + 2, r.height + 2), PopupBorder);
            EditorGUI.DrawRect(r, PopupBack);
            Wrap.Draw(r, content, false, false, false, false);
        }
    }
}
