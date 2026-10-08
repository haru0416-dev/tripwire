using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UdonIde.Editing
{
    /// <summary>
    /// The editing surface (IMGUI): only the lines on screen are drawn, token by token (rich text would read "&lt;b&gt;"
    /// in code as markup); positions come from IMGUI's text layout, cached per line.
    /// </summary>
    public sealed partial class CodeView : IMGUIContainer
    {
        public TextBuffer Buffer { get; private set; } = new TextBuffer();
        public event Action Changed;
        public event Action SaveRequested;
        /// <summary>F12 or Ctrl+click.</summary>
        public event Action<TextPos> DefinitionRequested;
        /// <summary>Shift+F12.</summary>
        public event Action<TextPos> ReferencesRequested;
        /// <summary>Alt+Left.</summary>
        public event Action BackRequested;
        public Func<TextPos, UdonIde.CompletionList> CompletionSource;
        public Func<UdonIde.Completion, string> CompletionDoc;
        public Func<TextPos, string> HoverSource;
        public bool ReadOnly;
        public List<Problem> Problems = new List<Problem>();

        public Font Font;
        public float FontSize = 13;
        const float Pad = 6, ScrollBar = 13;

        GUIStyle textStyle;
        float lineH, spaceW;
        Vector2 scroll;
        float contentW;
        int lastVersion = -1;
        LineStates states = new LineStates();
        readonly List<Token> tokens = new List<Token>();
        readonly GUIContent content = new GUIContent();
        readonly Dictionary<string, Dictionary<int, float>> xCache = new Dictionary<string, Dictionary<int, float>>();
        double blinkFrom;
        bool focusedLastPaint, wantKeyboard, dropKeyboard;
        static readonly int Hint = "UdonIdeCodeView".GetHashCode();

        static readonly HashSet<string> Commands = new HashSet<string> { "Copy", "Cut", "Paste", "SelectAll", "Duplicate", "Delete", "SoftDelete" };
        static readonly HashSet<string> EditingCommands = new HashSet<string> { "Cut", "Paste", "Delete", "SoftDelete", "Duplicate" };
        static readonly Dictionary<string, KeyCode> CommandKeys = new Dictionary<string, KeyCode>
            { { "Copy", KeyCode.C }, { "Cut", KeyCode.X }, { "Paste", KeyCode.V }, { "SelectAll", KeyCode.A }, { "Duplicate", KeyCode.D } };

        static readonly Color Back = new Color(0.12f, 0.12f, 0.13f), GutterText = new Color(0.45f, 0.45f, 0.48f),
            CurrentLine = new Color(1f, 1f, 1f, 0.04f), Selection = new Color(0.26f, 0.42f, 0.66f, 0.55f), SelectionUnfocused = new Color(0.4f, 0.4f, 0.45f, 0.35f),
            CaretColor = new Color(0.95f, 0.95f, 0.95f), ErrorLine = new Color(0.95f, 0.35f, 0.35f), WarningLine = new Color(0.9f, 0.75f, 0.3f);
        static readonly Color[] Colors =
        {
            new Color(0.86f, 0.86f, 0.86f), // plain
            new Color32(0x56, 0x9C, 0xD6, 0xFF), // keyword
            new Color32(0x4E, 0xC9, 0xB0, 0xFF), // type
            new Color32(0xCE, 0x91, 0x78, 0xFF), // string
            new Color32(0xB5, 0xCE, 0xA8, 0xFF), // number
            new Color32(0x6A, 0x99, 0x55, 0xFF), // comment
            new Color32(0xC5, 0x86, 0xC0, 0xFF), // preprocessor
        };

        public CodeView()
        {
            onGUIHandler = OnGUI;
            focusable = true;
            // UI Toolkit focus and IMGUI's keyboard control are separate: hand the keyboard to the text on focus.
            RegisterCallback<FocusInEvent>(_ => { wantKeyboard = true; MarkDirtyRepaint(); });
            RegisterCallback<FocusOutEvent>(_ => { wantKeyboard = false; dropKeyboard = true; MarkDirtyRepaint(); });
            style.flexGrow = 1;
            schedule.Execute(() => { if (focusedLastPaint) MarkDirtyRepaint(); }).Every(500);
            schedule.Execute(PollHover).Every(100);
        }

        public string Text
        {
            get => Buffer.Text;
            set { Buffer.Text = value; scroll = Vector2.zero; Edited(); }
        }

        public Vector2 Scroll { get => scroll; set { scroll = value; MarkDirtyRepaint(); } }

        /// <summary>A tab switch: no change event; the buffer keeps its caret and undo.</summary>
        public void SetBuffer(TextBuffer buffer, Vector2 scrollTo)
        {
            Buffer = buffer;
            states = new LineStates();
            scroll = scrollTo;
            CloseCompletion();
            hover = null;
            Edited(false);
        }

        void EnsureStyle()
        {
            if (textStyle != null) return;
            textStyle = new GUIStyle
            {
                font = Font,
                fontSize = (int)FontSize,
                richText = false,
                wordWrap = false,
                clipping = TextClipping.Overflow,
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
            };
            textStyle.normal.textColor = Colors[0];
            lineH = Mathf.Ceil(textStyle.lineHeight) + 3;
            spaceW = textStyle.CalcSize(new GUIContent("          ")).x / 10f;
        }

        /// <summary>After changing the buffer from outside.</summary>
        public void Edited() => Edited(true);

        void Edited(bool notify)
        {
            states.Invalidate(Buffer.TakeChangedFrom());
            lastVersion = Buffer.Version;
            // Width estimate for the horizontal scroll: wide (CJK) characters count twice.
            int widest = 0;
            foreach (var l in Buffer.Lines)
            {
                if (l.Length * 2 <= widest) continue;
                int w = 0; foreach (var c in l) w += c == '\t' ? TextBuffer.TabSize : c >= 0x1100 ? 2 : 1;
                widest = Math.Max(widest, w);
            }
            contentW = widest * (spaceW > 0 ? spaceW : 8) + 40;
            blinkFrom = EditorApplication.timeSinceStartup;
            if (notify) Changed?.Invoke();
            MarkDirtyRepaint();
        }

        // ---- text positions ----

        static string Display(string line, out int[] map)
        {
            map = null;
            if (line.IndexOf('\t') < 0) return line;
            var sb = new System.Text.StringBuilder();
            map = new int[line.Length + 1];
            for (int i = 0; i < line.Length; i++)
            {
                map[i] = sb.Length;
                if (line[i] == '\t') sb.Append(' ', TextBuffer.TabSize - sb.Length % TextBuffer.TabSize); else sb.Append(line[i]);
            }
            map[line.Length] = sb.Length;
            return sb.ToString();
        }

        float XOf(string display, int index)
        {
            if (index <= 0 || display.Length == 0) return 0;
            if (index > display.Length) index = display.Length;
            if (!xCache.TryGetValue(display, out var xs))
            {
                if (xCache.Count > 4000) xCache.Clear();
                xCache[display] = xs = new Dictionary<int, float>();
            }
            if (xs.TryGetValue(index, out var x)) return x;
            content.text = display;
            var r = new Rect(0, 0, 100000, lineH);
            x = textStyle.GetCursorPixelPosition(r, content, index).x;
            xs[index] = x;
            return x;
        }

        float ColX(int line, int col)
        {
            var d = Display(Buffer.Lines[line], out var map);
            return XOf(d, map != null ? map[Math.Min(col, map.Length - 1)] : col);
        }

        int ColAt(int line, float x)
        {
            var raw = Buffer.Lines[line];
            var d = Display(raw, out var map);
            if (x <= 0 || d.Length == 0) return 0;
            content.text = d;
            int di = textStyle.GetCursorStringIndex(new Rect(0, 0, 100000, lineH), content, new Vector2(x, lineH * 0.5f));
            if (map == null) return Math.Min(di, raw.Length);
            for (int i = 0; i <= raw.Length; i++) if (map[i] >= di) return i;
            return raw.Length;
        }

        // ---- layout ----

        float GutterWidth => (Math.Max(3, Buffer.LineCount.ToString().Length) + 2) * spaceW + 8;

        Rect TextRect(Rect all) => new Rect(all.x + GutterWidth, all.y, Math.Max(0, all.width - GutterWidth - ScrollBar), Math.Max(0, all.height - ScrollBar));

        int PageLines(Rect text) => Math.Max(1, (int)(text.height / lineH) - 1);

        TextPos HitTest(Rect text, Vector2 mouse)
        {
            float y = mouse.y - text.y - Pad + scroll.y;
            int line = Mathf.Clamp(Mathf.FloorToInt(y / lineH), 0, Buffer.LineCount - 1);
            if (y < 0) line = 0;
            float x = mouse.x - text.x - Pad + scroll.x;
            return new TextPos(line, ColAt(line, x));
        }

        void ScrollToCaret(Rect text)
        {
            float top = Buffer.Caret.Line * lineH, bottom = top + lineH + Pad * 2;
            if (top < scroll.y) scroll.y = top;
            if (bottom > scroll.y + text.height) scroll.y = bottom - text.height;
            float x = ColX(Buffer.Caret.Line, Buffer.Caret.Col) + Pad;
            if (x < scroll.x + Pad) scroll.x = Math.Max(0, x - Pad - 40);
            if (x > scroll.x + text.width - 20) scroll.x = x - text.width + 60;
        }

        float MaxScrollY(Rect text) => Math.Max(0, Buffer.LineCount * lineH + Pad * 2 - text.height + lineH * 3);
        float MaxScrollX(Rect text) => Math.Max(0, contentW - text.width);

        // ---- events ----

        void OnGUI()
        {
            EnsureStyle();
            var all = new Rect(0, 0, contentRect.width, contentRect.height);
            var text = TextRect(all);
            int id = GUIUtility.GetControlID(Hint, FocusType.Keyboard, all);
            if (wantKeyboard) { GUIUtility.keyboardControl = id; wantKeyboard = false; }
            if (dropKeyboard) { if (GUIUtility.keyboardControl == id) GUIUtility.keyboardControl = 0; dropKeyboard = false; }
            bool focused = GUIUtility.keyboardControl == id;
            var e = Event.current;
            if (Buffer.Version != lastVersion) Edited();

            if (focused)
            {
                Input.imeCompositionMode = IMECompositionMode.On;
                EditorGUIUtility.editingTextField = true; // keeps Unity's shortcuts (Ctrl+Z, Delete...) out while typing here
            }
            else if (focusedLastPaint)
            {
                // Focus left: the Scene view's WASD shouldn't go to the IME, and Unity's shortcuts work again.
                Input.imeCompositionMode = IMECompositionMode.Auto;
                EditorGUIUtility.editingTextField = false;
            }

            textArea = text;
            if (e.type == EventType.MouseMove || e.type == EventType.MouseLeaveWindow)
            {
                if ((e.mousePosition - hoverMouse).sqrMagnitude > 4 || e.type == EventType.MouseLeaveWindow)
                {
                    hoverMouse = e.type == EventType.MouseLeaveWindow ? new Vector2(-1000, -1000) : e.mousePosition;
                    hoverSince = EditorApplication.timeSinceStartup;
                    if (hover != null) { hover = null; MarkDirtyRepaint(); }
                }
            }
            if (e.type == EventType.KeyDown || e.type == EventType.MouseDown || e.type == EventType.ScrollWheel) { if (hover != null) { hover = null; MarkDirtyRepaint(); } hoverSince = EditorApplication.timeSinceStartup; }

            if (completion != null && HandleCompletionEvent(e, all, text)) return; // the list took the event

            switch (e.type)
            {
                case EventType.MouseDown when all.Contains(e.mousePosition) && e.button == 0 && (e.control || e.command) && text.Contains(e.mousePosition):
                {
                    GUIUtility.keyboardControl = id;
                    var p = HitTest(text, e.mousePosition);
                    Buffer.MoveTo(p, false);
                    DefinitionRequested?.Invoke(p);
                    e.Use();
                    break;
                }
                case EventType.MouseDown when all.Contains(e.mousePosition) && e.button == 0:
                {
                    CloseCompletion();
                    GUIUtility.keyboardControl = id;
                    GUIUtility.hotControl = id;
                    if (e.mousePosition.x < text.x)
                    {
                        Buffer.SelectLine(HitTest(text, e.mousePosition).Line); // the gutter selects lines
                    }
                    else if (text.Contains(e.mousePosition))
                    {
                        var p = HitTest(text, e.mousePosition);
                        if (e.clickCount == 2) Buffer.SelectWord(p);
                        else if (e.clickCount >= 3) Buffer.SelectLine(p.Line);
                        else Buffer.MoveTo(p, e.shift);
                    }
                    blinkFrom = EditorApplication.timeSinceStartup;
                    e.Use();
                    break;
                }
                case EventType.MouseDrag when GUIUtility.hotControl == id:
                {
                    var m = e.mousePosition;
                    if (m.y < text.y) scroll.y = Math.Max(0, scroll.y - lineH);
                    if (m.y > text.yMax) scroll.y = Math.Min(MaxScrollY(text), scroll.y + lineH);
                    Buffer.MoveTo(HitTest(text, m), true);
                    e.Use();
                    break;
                }
                case EventType.MouseUp when GUIUtility.hotControl == id:
                    GUIUtility.hotControl = 0;
                    e.Use();
                    break;
                case EventType.ScrollWheel when all.Contains(e.mousePosition):
                    if (e.shift) scroll.x = Mathf.Clamp(scroll.x + e.delta.y * spaceW * 3, 0, MaxScrollX(text));
                    else scroll.y = Mathf.Clamp(scroll.y + e.delta.y * lineH * 1.5f, 0, MaxScrollY(text));
                    e.Use();
                    break;
                case EventType.KeyDown when focused && e.keyCode == KeyCode.F12:
                    if (e.shift) ReferencesRequested?.Invoke(Buffer.Caret); else DefinitionRequested?.Invoke(Buffer.Caret);
                    e.Use();
                    break;
                case EventType.KeyDown when focused && e.alt && e.keyCode == KeyCode.LeftArrow:
                    BackRequested?.Invoke();
                    e.Use();
                    break;
                case EventType.KeyDown when focused && (e.control || e.command) && e.keyCode == KeyCode.Space:
                    OpenCompletion(true);
                    e.Use();
                    break;
                case EventType.KeyDown when focused:
                {
                    // AltGr (German {, Polish ż...) arrives as Ctrl+Alt on Windows: typing, not a shortcut.
                    bool ctrl = (e.control && !e.alt) || e.command;
                    if (ReadOnly && KeyMap.IsEdit(e.keyCode, e.character, ctrl)) { e.Use(); break; }
                    var before = Buffer.Caret;
                    var r = KeyMap.Apply(Buffer, e.keyCode, e.character, ctrl, e.shift, e.alt, PageLines(text),
                        () => EditorGUIUtility.systemCopyBuffer, s => EditorGUIUtility.systemCopyBuffer = s);
                    if (r == KeyResult.Ignored) break;
                    if (r == KeyResult.Save) SaveRequested?.Invoke();
                    if (r == KeyResult.Edited) Edited();
                    AfterKey(e, r, before);
                    ScrollToCaret(text);
                    blinkFrom = EditorApplication.timeSinceStartup;
                    e.Use();
                    break;
                }
                // Read-only: taken so Unity doesn't run them elsewhere, then dropped.
                case EventType.ValidateCommand when focused && ReadOnly && EditingCommands.Contains(e.commandName):
                    e.Use();
                    break;
                case EventType.ExecuteCommand when focused && ReadOnly && EditingCommands.Contains(e.commandName):
                    e.Use();
                    break;
                case EventType.ValidateCommand when focused:
                    if (Commands.Contains(e.commandName)) e.Use();
                    break;
                case EventType.ExecuteCommand when focused:
                {
                    CloseCompletion();
                    KeyResult r;
                    if (e.commandName == "Delete" || e.commandName == "SoftDelete") { Buffer.Delete(); r = KeyResult.Edited; }
                    else if (!CommandKeys.TryGetValue(e.commandName, out var key)) break;
                    else r = KeyMap.Apply(Buffer, key, '\0', true, false, false, PageLines(text), () => EditorGUIUtility.systemCopyBuffer, s => EditorGUIUtility.systemCopyBuffer = s);
                    if (r == KeyResult.Edited) Edited();
                    ScrollToCaret(text);
                    e.Use();
                    break;
                }
            }

            // Scroll bars last: on top, and they handle their own events.
            if (e.type == EventType.Repaint) Draw(all, text, focused);
            float maxY = MaxScrollY(text), maxX = MaxScrollX(text);
            scroll.y = GUI.VerticalScrollbar(new Rect(all.xMax - ScrollBar, all.y, ScrollBar, all.height - ScrollBar), Mathf.Min(scroll.y, maxY), text.height, 0, maxY + text.height);
            scroll.x = GUI.HorizontalScrollbar(new Rect(text.x, all.yMax - ScrollBar, text.width, ScrollBar), Mathf.Min(scroll.x, maxX), text.width, 0, maxX + text.width);

            focusedLastPaint = focused;
        }

        // ---- drawing ----

        void Draw(Rect all, Rect text, bool focused)
        {
            EditorGUI.DrawRect(all, Back);
            int first = Math.Max(0, (int)((scroll.y - Pad) / lineH));
            int last = Math.Min(Buffer.LineCount - 1, (int)((scroll.y + text.height) / lineH) + 1);
            float textTop = (lineH - textStyle.lineHeight) * 0.5f;

            var gutter = new Rect(all.x, all.y, GutterWidth, text.height);
            EditorGUI.DrawRect(gutter, Back);
            GUI.BeginClip(gutter);
            var numberStyle = new GUIStyle(textStyle) { alignment = TextAnchor.UpperRight };
            for (int l = first; l <= last; l++)
            {
                numberStyle.normal.textColor = l == Buffer.Caret.Line ? Colors[0] : GutterText;
                content.text = (l + 1).ToString();
                numberStyle.Draw(new Rect(0, Pad + l * lineH - scroll.y + textTop, gutter.width - 10, lineH), content, false, false, false, false);
            }
            GUI.EndClip();

            GUI.BeginClip(text);
            float ox = Pad - scroll.x, oy = Pad - scroll.y;

            EditorGUI.DrawRect(new Rect(0, oy + Buffer.Caret.Line * lineH, text.width, lineH), CurrentLine);
            if (Buffer.HasSelection)
            {
                var s = Buffer.SelStart; var en = Buffer.SelEnd;
                for (int l = Math.Max(first, s.Line); l <= Math.Min(last, en.Line); l++)
                {
                    float x0 = l == s.Line ? ColX(l, s.Col) : 0;
                    float x1 = l == en.Line ? ColX(l, en.Col) : ColX(l, Buffer.Lines[l].Length) + spaceW * 0.6f;
                    EditorGUI.DrawRect(new Rect(ox + x0, oy + l * lineH, Math.Max(1, x1 - x0), lineH), focused ? Selection : SelectionUnfocused);
                }
            }

            for (int l = first; l <= last; l++)
            {
                var raw = Buffer.Lines[l];
                if (raw.Length == 0) continue;
                LineLexer.Lex(raw, states.StartOf(Buffer, l), tokens);
                var d = Display(raw, out var map);
                float y = oy + l * lineH + textTop;
                foreach (var t in tokens)
                {
                    int a = map != null ? map[t.Start] : t.Start, b = map != null ? map[t.Start + t.Length] : t.Start + t.Length;
                    if (IsBlank(d, a, b)) continue;
                    float x0 = XOf(d, a);
                    if (ox + x0 > text.width) break;
                    float x1 = XOf(d, b);
                    if (ox + x1 < 0) continue;
                    textStyle.normal.textColor = Colors[(int)t.Kind];
                    content.text = d.Substring(a, b - a);
                    textStyle.Draw(new Rect(ox + x0, y, x1 - x0 + 20, lineH), content, false, false, false, false);
                }
            }
            textStyle.normal.textColor = Colors[0];

            foreach (var p in Problems)
            {
                if (p.File != null || p.Line < first || p.Line > last || p.Line >= Buffer.LineCount) continue;
                int lineLen = Buffer.Lines[p.Line].Length;
                int c0 = Math.Min(p.Column, lineLen), c1 = Math.Min(lineLen, p.Column + Math.Max(1, p.Length));
                float x0 = ColX(p.Line, c0), x1 = c1 > c0 ? ColX(p.Line, c1) : x0 + spaceW;
                Squiggle(ox + x0, ox + x1, oy + (p.Line + 1) * lineH - 2, p.Error ? ErrorLine : WarningLine);
            }

            float cx = ox + ColX(Buffer.Caret.Line, Buffer.Caret.Col), cy = oy + Buffer.Caret.Line * lineH;
            var composing = focused ? Input.compositionString : "";
            if (!string.IsNullOrEmpty(composing))
            {
                content.text = composing;
                var size = textStyle.CalcSize(content);
                EditorGUI.DrawRect(new Rect(cx, cy, size.x + 2, lineH), Back);
                textStyle.Draw(new Rect(cx, cy + textTop, size.x + 20, lineH), content, false, false, false, false);
                EditorGUI.DrawRect(new Rect(cx, cy + lineH - 2, size.x, 1), Colors[0]);
                cx += size.x;
            }
            if (focused)
            {
                // Where the IME puts its candidate window: under the caret, in screen points.
                Input.compositionCursorPos = GUIUtility.GUIToScreenPoint(new Vector2(cx, cy + lineH));
                bool on = ((EditorApplication.timeSinceStartup - blinkFrom) % 1.0) < 0.6;
                if (on) EditorGUI.DrawRect(new Rect(cx, cy + 1, 1.5f, lineH - 2), CaretColor);
            }
            GUI.EndClip();
            if (ReadOnly) { content.text = "読み取り専用"; var sz = textStyle.CalcSize(content); GUI.Label(new Rect(text.xMax - sz.x - 8, text.y + 2, sz.x, lineH), content, Dim); }
            if (completion != null) DrawCompletion(all, new Vector2(text.x + cx, text.y + cy));
            if (hover != null) DrawHover(all);
        }

        static bool IsBlank(string s, int a, int b)
        {
            for (int i = a; i < b; i++) if (!char.IsWhiteSpace(s[i])) return false;
            return true;
        }

        static void Squiggle(float x0, float x1, float y, Color color)
        {
            bool up = false;
            for (float x = x0; x < x1; x += 2, up = !up) EditorGUI.DrawRect(new Rect(x, up ? y - 1 : y, 2, 1), color);
        }

        // ---- popups ----

        static GUIStyle dim, wrap;
        GUIStyle Dim => dim ??= new GUIStyle(textStyle) { normal = { textColor = new Color(0.55f, 0.55f, 0.58f) } };
        GUIStyle Wrap => wrap ??= new GUIStyle(textStyle) { wordWrap = true, clipping = TextClipping.Clip, normal = { textColor = new Color(0.8f, 0.8f, 0.82f) }, padding = new RectOffset(8, 8, 6, 6) };
        static readonly Color PopupBack = new Color(0.16f, 0.16f, 0.18f), PopupBorder = new Color(0.32f, 0.32f, 0.36f), PopupSelected = new Color(0.22f, 0.32f, 0.48f);

        // ---- for the window and tests ----

        public void SetCaret(TextPos caret, TextPos? anchor = null)
        {
            Buffer.Caret = Buffer.Clamp(caret);
            Buffer.Anchor = anchor.HasValue ? Buffer.Clamp(anchor.Value) : Buffer.Caret;
            var all = new Rect(0, 0, contentRect.width, contentRect.height);
            if (textStyle != null) ScrollToCaret(TextRect(all));
            MarkDirtyRepaint();
        }

        public void FocusText() { Focus(); MarkDirtyRepaint(); }

        internal string Describe() => $"lineH {lineH} spaceW {spaceW} scroll {scroll} contentW {contentW} lines {Buffer.LineCount} font {(Font != null ? Font.name : "null")}";
    }
}
