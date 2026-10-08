using System.Collections.Generic;
using System.Linq;
using Tripwire.Core;
using UnityEditor;
using UnityEngine;

namespace Tripwire.Editor
{
    // The trigger Inspector's look: category colors (Okabe–Ito, readable with color-vision differences), card stripes,
    // the "いつ" tag, action icons, section headers.
    internal sealed partial class TripwireTriggerEditor
    {
        static readonly Color Blue = Hex(0x0072B2), Sky = Hex(0x56B4E9), Green = Hex(0x009E73), Orange = Hex(0xE69F00),
            Vermillion = Hex(0xD55E00), Pink = Hex(0xCC79A7), Yellow = Hex(0xD9C700), Grey = Hex(0x8A8A8A), Purple = Hex(0x8C6BC8);

        /// <summary>Secondary text: light grey on the dark skin, dark grey on the light one (light grey there is unreadable).</summary>
        internal static Color Muted(float onDark, float onLight) { float v = EditorGUIUtility.isProSkin ? onDark : onLight; return new Color(v, v, v); }

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);

        /// <summary>A category's color: the same hue across events and actions where they mean the same thing.</summary>
        internal static Color CategoryColor(string category)
        {
            switch (category)
            {
                case "Common": case "Show": return Blue;
                case "Pickup": return Orange;
                case "Player": return Green;
                case "Physics": case "Move": return Sky;
                case "Avatar": return Pink;
                case "Input": case "Flow": return Yellow;
                case "UI": case "Text": return Purple;
                case "Video": return Vermillion;
                case "SoundFx": return Orange;
                case "Variable": case "Network": return Pink;
                case "Link": return Green;
                default: return Grey;
            }
        }

        /// <summary>A thin stripe in the category's color along a card's left edge.</summary>
        static void Stripe(Rect card, Color color)
        {
            if (Event.current.type == EventType.Repaint) EditorGUI.DrawRect(new Rect(card.x + 1, card.y + 3, 3, card.height - 6), color);
        }

        static GUIStyle tagStyle, titleStyle, rowNote, rowButton, rowButtonLeft, rowButtonMid, rowButtonRight, rowPopup;

        /// <summary>The height of header lines (the Inspector header, card headers): everything on them is centered in it.</summary>
        const float RowHeight = 20;

        static void InitRowStyles()
        {
            if (rowButton != null) return;
            GUIStyle Row(GUIStyle from) => new GUIStyle(from) { fixedHeight = RowHeight, alignment = TextAnchor.MiddleCenter, margin = new RectOffset(from.margin.left, from.margin.right, 0, 0) };
            rowButton = Row(EditorStyles.miniButton);
            rowButtonLeft = Row(EditorStyles.miniButtonLeft);
            rowButtonMid = Row(EditorStyles.miniButtonMid);
            rowButtonRight = Row(EditorStyles.miniButtonRight);
            rowPopup = new GUIStyle(EditorStyles.popup) { fixedHeight = RowHeight, margin = new RectOffset(4, 4, 0, 0) };
            rowNote = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleLeft, fixedHeight = RowHeight, margin = new RectOffset(2, 2, 0, 0) };
            rowNote.normal.textColor = Muted(0.6f, 0.34f);
        }

        /// <summary>A small colored tag ("いつ").</summary>
        static void Tag(string text, Color color)
        {
            if (tagStyle == null)
            {
                tagStyle = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, padding = new RectOffset(4, 4, 0, 0), margin = new RectOffset(2, 2, 0, 0) };
                tagStyle.normal.textColor = Color.white;
            }
            var content = new GUIContent(text);
            // A row-high slot with the tag centered in it, so it lines up with the fields beside it.
            var slot = GUILayoutUtility.GetRect(content, tagStyle, GUILayout.Width(tagStyle.CalcSize(content).x + 4), GUILayout.Height(RowHeight));
            var r = new Rect(slot.x, slot.y + 2, slot.width, RowHeight - 4);
            if (Event.current.type == EventType.Repaint)
            {
                var fill = new Color(color.r * 0.8f, color.g * 0.8f, color.b * 0.8f, 1f);
                EditorGUI.DrawRect(r, fill);
                // Dark text on light tags (yellow), white on the others.
                tagStyle.normal.textColor = 0.299f * fill.r + 0.587f * fill.g + 0.114f * fill.b > 0.55f ? new Color(0.1f, 0.1f, 0.1f) : Color.white;
                tagStyle.Draw(r, content, false, false, false, false);
            }
        }

        /// <summary>The label of a block's list (Then, Else, Do), as a tag in the flow color.</summary>
        static void BlockTag(string text)
        {
            GUILayout.Space(2);
            using (new EditorGUILayout.HorizontalScope()) { Tag(text, Yellow); GUILayout.FlexibleSpace(); }
        }

        // Lucide icons (Icons/LUCIDE-LICENSE.txt): white lines, drawn in the category's color.
        static readonly Dictionary<string, Texture2D> icons = new Dictionary<string, Texture2D>();

        internal static Texture2D Icon(string path)
        {
            if (!icons.TryGetValue(path, out var icon)) icons[path] = icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconDir + path + ".png");
            return icon;
        }

        /// <summary>A category's icon, or null.</summary>
        internal static Texture2D CategoryIcon(string category) => string.IsNullOrEmpty(category) ? null : Icon("Categories/" + category);

        /// <summary>An event's own icon, else its category's.</summary>
        internal static Texture2D EventIcon(EventSpec spec) => spec == null ? null : Icon("Events/" + spec.Id) ?? CategoryIcon(spec.Category);

        /// <summary>An action's own icon, else its category's.</summary>
        internal static Texture2D ActionIcon(ActionSpec spec) => spec == null ? null : Icon("Actions/" + spec.Id) ?? CategoryIcon(spec.Category);

        /// <summary>Draws a white-line icon in a color (a little lighter on the dark skin, where thin dark lines vanish).</summary>
        internal static void DrawTinted(Rect r, Texture icon, Color color)
        {
            if (EditorGUIUtility.isProSkin) color = Color.Lerp(color, Color.white, 0.25f);
            if (icon != null && Event.current.type == EventType.Repaint) GUI.DrawTexture(r, icon, ScaleMode.ScaleToFit, true, 0, color, 0, 0);
        }

        /// <summary>A row-high slot with a 16 px icon centered in it, in a color.</summary>
        static void IconSlot(Texture icon, Color tint)
        {
            var slot = GUILayoutUtility.GetRect(18, RowHeight, GUILayout.Width(18), GUILayout.Height(RowHeight));
            DrawTinted(new Rect(slot.x + 1, slot.y + (slot.height - 16) / 2, 16, 16), icon, tint);
        }

        /// <summary>A section title with a rule under it (as VizVid and ProTV separate their settings).</summary>
        void SectionHeader(string title, System.Action right = null)
        {
            GUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(title, boldLabel);
                GUILayout.FlexibleSpace();
                right?.Invoke();
            }
            var r = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint) EditorGUI.DrawRect(r, new Color(0.5f, 0.5f, 0.5f, 0.35f));
            GUILayout.Space(3);
        }

        const string IconDir = "Packages/dev.haru0416.tripwire/Editor/Icons/";
        static Texture2D logoMark;
        /// <summary>The logo in the accent color, 32 px for 16 px places (header, window tab; also the component's icon).</summary>
        internal static Texture2D LogoMark => logoMark != null ? logoMark : logoMark = AssetDatabase.LoadAssetAtPath<Texture2D>(IconDir + "TripwireMark.png");

        /// <summary>The title of the header: an accent bar, the logo and the name.</summary>
        static void HeaderTitle()
        {
            InitRowStyles();
            if (titleStyle == null)
            {
                // The left padding leaves room for the logo, drawn separately at its own pixel size.
                titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, alignment = TextAnchor.MiddleLeft, fixedHeight = RowHeight, margin = new RectOffset(2, 2, 0, 0), padding = new RectOffset(22, 2, 0, 2) };
                titleStyle.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.92f, 0.92f, 0.92f) : new Color(0.12f, 0.12f, 0.12f);
            }
            var r = GUILayoutUtility.GetRect(new GUIContent("Tripwire"), titleStyle, GUILayout.ExpandWidth(false), GUILayout.Height(RowHeight));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(new Rect(r.x, r.yMax - 2, r.width, 2), Sky); // inside the row
                if (LogoMark != null) GUI.DrawTexture(new Rect(r.x + 2, r.y + 1, 16, 16), LogoMark, ScaleMode.ScaleToFit);
            }
            GUI.Label(r, new GUIContent("Tripwire", T("VRChat gimmicks, built in the Inspector", "VRChat のギミックを Inspector で")), titleStyle);
        }
    }
}
