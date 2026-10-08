using System;
using System.Collections.Generic;
using System.Linq;
using Tripwire.Core;
using UnityEditor;
using UnityEngine;

namespace Tripwire.Editor
{
    /// <summary>
    /// Two-column picker for events and actions: purpose categories on the left, entries with a one-line description on
    /// the right, a search box on top. The "Advanced" category sits last, below a separator. A "★" category first
    /// lists starred entries and the last few picked (kept per user in EditorPrefs).
    /// </summary>
    internal sealed class CatalogPicker : PopupWindowContent
    {
        public sealed class Entry
        {
            public string Id, Name, Description, Category;
            /// <summary>The name in Udon / Unity (OnPickup, GameObject.SetActive), shown small after the name.</summary>
            public string Code;
            /// <summary>Drawn before the name, in the category's color.</summary>
            public Texture Icon;
        }

        readonly IReadOnlyList<Entry> entries;
        string[] categories;
        readonly Action<string> onPick;
        readonly string memoryKey;
        string category;
        const string Favorites = "★";
        const int RecentCount = 6;
        string search = "";
        HashSet<string> starredNow = new HashSet<string>();
        Vector2 scroll;
        readonly string[] allCategories;
        /// <summary>The search result chosen with the arrow keys (Enter picks it).</summary>
        int cursor;
        string cursorSearch;

        /// <summary>Categories only "くわしく" lists.</summary>

        static GUIStyle itemStyle, nameStyle, descStyle, noteStyle, categoryStyle, categorySelectedStyle, headerStyle;

        public CatalogPicker(IReadOnlyList<Entry> entries, string[] categories, Action<string> onPick)
        {
            this.entries = entries;
            allCategories = categories;
            // かんたん: the categories most gimmicks use (search still finds everything, favorites still show).
            this.categories = TripwireSettings.Detailed ? categories : categories.Where(c => !Texts.AdvancedCategories.Contains(c)).ToArray();
            this.onPick = onPick;
            memoryKey = entries.Any(e => EventCatalog.Get(e.Id) != null) ? "Events" : "Actions";
            category = Starred().Count + Recent().Count > 0 ? Favorites : categories[0];
        }

        // ---- favorites and recent picks ----
        /// <summary>Saved ids, including ones this version doesn't know (kept for other projects / versions).</summary>
        List<string> LoadAll(string what) =>
            EditorPrefs.GetString("Tripwire." + what + "." + memoryKey, "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();

        /// <summary>Saved ids this version can show.</summary>
        List<string> Load(string what) => LoadAll(what).Where(id => entries.Any(e => e.Id == id)).ToList();

        void Save(string what, List<string> ids) => EditorPrefs.SetString("Tripwire." + what + "." + memoryKey, string.Join("\n", ids));
        List<string> Starred() => Load("Favorites");
        List<string> Recent() => Load("Recent");

        void ToggleStar(string id)
        {
            var list = LoadAll("Favorites");
            if (!list.Remove(id)) list.Add(id);
            Save("Favorites", list);
        }

        void Remember(string id)
        {
            var list = LoadAll("Recent");
            list.Remove(id);
            list.Insert(0, id);
            Save("Recent", list.Take(RecentCount).ToList());
        }

        /// <summary>The catalog's actions, and (when adding) the actions saved as assets.</summary>
        public static IReadOnlyList<Entry> Actions(bool withSaved = false) =>
            ActionCatalog.InMenuOrder.Select(a => new Entry { Id = a.Id, Name = Texts.ActionName(a), Description = Texts.ActionDescription(a), Category = a.Category, Code = Texts.ActionCode(a.Id), Icon = TripwireTriggerEditor.ActionIcon(a) })
                .Concat(withSaved
                    ? TripwireActionSet.All().Select(x => new Entry { Id = TripwireActionSet.IdPrefix + x.guid, Name = x.set.name, Description = x.set.description, Category = "Saved", Code = "", Icon = TripwireTriggerEditor.CategoryIcon("Saved") })
                    : Enumerable.Empty<Entry>())
                .ToList();

        public static IReadOnlyList<Entry> Events() =>
            EventCatalog.InMenuOrder.Select(e => new Entry { Id = e.Id, Name = Texts.EventName(e), Description = Texts.EventDescription(e), Category = e.Category, Code = Texts.EventCode(e),
                Icon = TripwireTriggerEditor.EventIcon(e) }).ToList();


        public override Vector2 GetWindowSize() => new Vector2(640, 420);

        static void InitStyles()
        {
            if (itemStyle != null) return;
            itemStyle = new GUIStyle("MenuItem") { fixedHeight = 0, padding = new RectOffset(8, 8, 4, 4) };
            nameStyle = new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold };
            descStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
            descStyle.normal.textColor = TripwireTriggerEditor.Muted(0.62f, 0.32f);
            noteStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = false, clipping = TextClipping.Clip };
            noteStyle.normal.textColor = TripwireTriggerEditor.Muted(0.5f, 0.4f);
            // The left padding leaves room for the category icon.
            categoryStyle = new GUIStyle("MenuItem") { fixedHeight = 22, padding = new RectOffset(30, 8, 3, 3) };
            categorySelectedStyle = new GUIStyle(categoryStyle);
            categorySelectedStyle.normal.textColor = Color.white;
            headerStyle = new GUIStyle(EditorStyles.miniLabel);
            headerStyle.normal.textColor = TripwireTriggerEditor.Muted(0.55f, 0.36f);
        }

        public override void OnOpen() => EditorGUI.FocusTextInControl("tw-search");

        public override void OnGUI(Rect rect)
        {
            InitStyles();
            starredNow = new HashSet<string>(Starred()); // read once per pass, not once per item
            GUILayout.BeginArea(new Rect(rect.x + 4, rect.y + 4, rect.width - 8, rect.height - 8));
            var q = search.Trim();
            var hits = q.Length == 0 ? null : entries.Where(e => Contains(e.Name, q) || Contains(e.Description, q) || Contains(e.Id, q) || Contains(e.Code, q)).ToList();
            if (cursorSearch != q) { cursorSearch = q; cursor = 0; }
            // Arrow keys and Enter in the results, before the search field takes the keys.
            var key = Event.current;
            if (hits != null && hits.Count > 0 && key.type == EventType.KeyDown)
            {
                if (key.keyCode == KeyCode.DownArrow) { cursor = Mathf.Min(cursor + 1, hits.Count - 1); key.Use(); }
                else if (key.keyCode == KeyCode.UpArrow) { cursor = Mathf.Max(cursor - 1, 0); key.Use(); }
                else if (key.keyCode == KeyCode.Return || key.keyCode == KeyCode.KeypadEnter) { key.Use(); Pick(hits[Mathf.Clamp(cursor, 0, hits.Count - 1)].Id); GUIUtility.ExitGUI(); }
            }
            GUI.SetNextControlName("tw-search");
            search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
            GUILayout.Space(4);

            if (hits != null)
            {
                scroll = GUILayout.BeginScrollView(scroll);
                if (hits.Count == 0) GUILayout.Label(Texts.T("Nothing found.", "見つかりません。"), headerStyle);
                for (int i = 0; i < hits.Count; i++) Item(hits[i], true, wide: true, chosen: i == cursor);
                GUILayout.EndScrollView();
            }
            else
            {
                using (new GUILayout.HorizontalScope())
                {
                    using (new GUILayout.VerticalScope(GUILayout.Width(170)))
                    {
                        if (CategoryButton(Favorites)) category = Favorites;
                        foreach (var c in categories)
                        {
                            if (c == "Advanced")
                            {
                                GUILayout.Space(4);
                                GUILayout.Box(GUIContent.none, GUILayout.Height(1), GUILayout.ExpandWidth(true));
                                GUILayout.Space(2);
                            }
                            if (CategoryButton(c)) category = c;
                        }
                        // かんたん hides some categories: say how much, and how to see it.
                        var hiddenEntries = TripwireSettings.Detailed ? new List<Entry>() : entries.Where(e => Texts.AdvancedCategories.Contains(e.Category)).ToList();
                        if (hiddenEntries.Count > 0)
                        {
                            GUILayout.FlexibleSpace();
                            var kinds = string.Join(Texts.T(", ", "、"), hiddenEntries.Select(e => e.Category).Distinct().Take(3).Select(Texts.Category));
                            // No-break space: the number stays with its word when the line wraps.
                            GUILayout.Label(Texts.T(hiddenEntries.Count + "\u00A0more in Detailed (" + kinds + ")", "かんたんでは\u00A0" + hiddenEntries.Count + "\u00A0件を隠しています（" + kinds + "）"), EditorStyles.wordWrappedMiniLabel);
                            if (GUILayout.Button(Texts.T("Show in Detailed", "くわしくで表示"), EditorStyles.miniButton))
                            {
                                TripwireSettings.Detailed = true;
                                categories = allCategories;
                            }
                        }
                    }
                    using (new GUILayout.VerticalScope())
                    {
                        GUILayout.Label(CategoryName(category), headerStyle);
                        scroll = GUILayout.BeginScrollView(scroll);
                        if (category == Favorites) DrawFavorites();
                        else foreach (var e in entries.Where(x => x.Category == category)) Item(e, false);
                        GUILayout.EndScrollView();
                    }
                }
            }
            GUILayout.EndArea();
            if (Event.current.type == EventType.MouseMove) editorWindow.Repaint();
        }

        static string CategoryName(string c) => c == Favorites ? Texts.T("Favorites & recent", "お気に入り・最近") : Texts.Category(c);

        void DrawFavorites()
        {
            var starred = Starred();
            var recent = Recent().Where(id => !starred.Contains(id)).ToList();
            if (starred.Count == 0 && recent.Count == 0)
                GUILayout.Label(Texts.T("Click ☆ on an entry to keep it here. Recent picks show up here too.",
                                        "項目の ☆ を押すとここに置けます。最近選んだものもここに出ます。"), descStyle);
            foreach (var id in starred) Item(entries.First(e => e.Id == id), true);
            if (recent.Count > 0)
            {
                GUILayout.Label(Texts.T("Recent", "最近使ったもの"), headerStyle);
                foreach (var id in recent) Item(entries.First(e => e.Id == id), true);
            }
        }

        bool CategoryButton(string c)
        {
            var content = new GUIContent(CategoryName(c) + "  ›");
            var r = GUILayoutUtility.GetRect(content, categoryStyle, GUILayout.ExpandWidth(true));
            bool selected = c == category;
            if (Event.current.type == EventType.Repaint)
            {
                // Selected: the selection color behind white text. (The menu style's "on" state would draw a check mark
                // over the start of the text.)
                if (selected) EditorGUI.DrawRect(r, EditorGUIUtility.isProSkin ? new Color(0.17f, 0.36f, 0.53f) : new Color(0.23f, 0.45f, 0.69f));
                (selected ? categorySelectedStyle : categoryStyle).Draw(r, content, !selected && r.Contains(Event.current.mousePosition), false, false, false);
                var icon = TripwireTriggerEditor.CategoryIcon(c == Favorites ? "Favorites" : c);
                var color = selected ? Color.white : c == Favorites ? new Color(0.9f, 0.75f, 0.2f) : TripwireTriggerEditor.CategoryColor(c);
                TripwireTriggerEditor.DrawTinted(new Rect(r.x + 8, r.y + (r.height - 16) / 2, 16, 16), icon, color);
            }
            if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
                scroll = Vector2.zero;
                return true;
            }
            return false;
        }

        /// <param name="wide">Shown across the whole picker (search results), not in the right-hand column.</param>
        void Item(Entry e, bool showCategory, bool wide = false, bool chosen = false)
        {
            var name = e.Name;
            // Small grey note after the name: the Udon / Unity name, and the category where entries are mixed.
            // Udon / Unity names in "くわしく" only.
            var note = string.Join(" · ", new[] { TripwireSettings.Detailed && e.Code != e.Name ? e.Code : null, showCategory ? Texts.Category(e.Category) : null }.Where(x => !string.IsNullOrEmpty(x)));
            float descHeight = descStyle.CalcHeight(new GUIContent(e.Description), wide ? 578 : 408);
            var r = GUILayoutUtility.GetRect(0, 22 + descHeight, GUILayout.ExpandWidth(true));
            bool hover = r.Contains(Event.current.mousePosition);
            var star = new Rect(r.xMax - 24, r.y + 2, 20, 18);
            bool starred = starredNow.Contains(e.Id);
            if (Event.current.type == EventType.Repaint)
            {
                itemStyle.Draw(r, GUIContent.none, hover || chosen, false, false, false);
                var icon = new Rect(r.x + 8, r.y + 4, 16, 16);
                TripwireTriggerEditor.DrawTinted(icon, e.Icon, TripwireTriggerEditor.CategoryColor(e.Category));
                const float text = 30; // the name and description start after the icon
                GUI.Label(new Rect(r.x + text, r.y + 3, r.width - text - 28, 18), name, nameStyle);
                if (note.Length > 0)
                {
                    float nameWidth = nameStyle.CalcSize(new GUIContent(name)).x;
                    GUI.Label(new Rect(r.x + text + 6 + nameWidth, r.y + 4, Mathf.Max(0, r.width - text - 42 - nameWidth), 16), note, noteStyle);
                }
                GUI.Label(new Rect(r.x + text, r.y + 20, r.width - text - 4, descHeight), e.Description, descStyle);
                if (starred || hover)
                {
                    var color = GUI.contentColor;
                    GUI.contentColor = starred ? new Color(1f, 0.8f, 0.2f) : TripwireTriggerEditor.Muted(0.6f, 0.4f);
                    GUI.Label(star, starred ? "★" : "☆", nameStyle);
                    GUI.contentColor = color;
                }
            }
            if (Event.current.type == EventType.MouseDown && hover)
            {
                Event.current.Use();
                if (star.Contains(Event.current.mousePosition))
                {
                    ToggleStar(e.Id); // keep the picker open
                    editorWindow.Repaint();
                    return;
                }
                Pick(e.Id);
            }
        }

        void Pick(string id)
        {
            Remember(id);
            onPick(id);
            editorWindow.Close();
        }

        static bool Contains(string s, string q) => s != null && s.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
