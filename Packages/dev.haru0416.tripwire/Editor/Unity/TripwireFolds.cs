using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Tripwire.Editor
{
    /// <summary>
    /// Which event cards are folded. Editor state, kept out of the scene: folding isn't an edit, so it makes no undo
    /// step and no unsaved scene. Cards are told apart by <see cref="KEvent.id"/>.
    /// </summary>
    internal static class TripwireFolds
    {
        const string Key = "Tripwire.FoldedCards";
        const int Keep = 4000;
        static HashSet<string> folded;

        static HashSet<string> Folded => folded ??= new HashSet<string>(EditorPrefs.GetString(Key, "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));

        /// <summary>
        /// Gives cards without an id one. Not recorded as an edit (it is saved with the scene's next save); a card
        /// folded in data from before this moved here stays folded.
        /// </summary>
        public static void EnsureIds(TripwireTrigger t)
        {
            if (t == null) return;
            bool added = false;
            foreach (var e in t.events)
            {
                if (e == null || !string.IsNullOrEmpty(e.id)) continue;
                e.id = Guid.NewGuid().ToString("N").Substring(0, 12);
                if (!e.expanded) { Folded.Add(e.id); added = true; }
            }
            if (added) Save();
        }

        public static bool IsExpanded(KEvent e) => string.IsNullOrEmpty(e.id) || !Folded.Contains(e.id);

        public static void SetExpanded(KEvent e, bool expanded)
        {
            if (string.IsNullOrEmpty(e.id)) return;
            if (expanded ? !Folded.Remove(e.id) : !Folded.Add(e.id)) return;
            Save();
        }

        static void Save()
        {
            // Oldest entries are dropped first only by chance (a set has no order); the cap just keeps the pref small.
            if (Folded.Count > Keep) folded = new HashSet<string>(Folded.Skip(Folded.Count - Keep));
            EditorPrefs.SetString(Key, string.Join(",", Folded));
        }
    }
}
