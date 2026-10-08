using System;
using System.Reflection;
using Tripwire.Core;
using UnityEditor;

namespace Tripwire.Editor
{
    /// <summary>
    /// Tools > Tripwire in the display language. [MenuItem] names are fixed when compiled, so the items are declared in
    /// English and, for Japanese, swapped through Unity's internal Menu.AddMenuItem / RemoveMenuItem: after each script
    /// reload, on a language change, and whenever Unity rebuilds its menus. Without that API the menu stays English.
    /// </summary>
    [InitializeOnLoad]
    internal static class TripwireMenus
    {
        internal const string ApplyAll = "Tools/Tripwire/Apply All Triggers";
        internal const string List = "Tools/Tripwire/Trigger List";
        internal const string DeleteUnused = "Tools/Tripwire/Delete Unused Generated Scripts";
        internal const string History = "Tools/Tripwire/Event History";
        internal const string Export = "Tools/Tripwire/Export for Distribution…";
        internal const string ExportFromAssets = "Assets/Tripwire/Export for Distribution…";

        internal static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode; // play-mode copies would be changed

        // Priorities as declared on the [MenuItem]s (around Unity's default 1000, which keeps Tools where it was).
        static readonly (string En, string Ja, int Priority, Action Run, Func<bool> Enabled)[] Items =
        {
            (ApplyAll, "Tools/Tripwire/すべて反映", 1000, TripwireCompiler.ApplyAllMenu, NotPlaying),
            (List, "Tools/Tripwire/トリガー一覧", 1001, TripwireOverviewWindow.Open, () => true),
            (History, "Tools/Tripwire/実行の記録", 1002, TripwireHistoryWindow.Open, () => true),
            (Export, "Tools/Tripwire/配布用に書き出す…", 1010, TripwireExportWindow.Open, NotPlaying),
            (ExportFromAssets, "Assets/Tripwire/配布用に書き出す…", 1100, TripwireExportWindow.Open, NotPlaying),
            (DeleteUnused, "Tools/Tripwire/使っていない生成スクリプトを消す", 1020, TripwireCompiler.DeleteUnusedMenu, NotPlaying),
        };

        const BindingFlags Internal = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        static readonly MethodInfo Add = typeof(Menu).GetMethod("AddMenuItem", Internal);
        static readonly MethodInfo Remove = typeof(Menu).GetMethod("RemoveMenuItem", Internal);
        static readonly MethodInfo Exists = typeof(Menu).GetMethod("MenuItemExists", Internal);
        static readonly MethodInfo UpdateBar = typeof(EditorUtility).GetMethod("Internal_UpdateAllMenus", Internal); // redraws the menu bar
        static bool refreshing;

        static TripwireMenus()
        {
            if (Add == null || Remove == null) return;
            EditorApplication.delayCall += Refresh;
            // A rebuild (a package import, a shortcut change) brings back the declared English items.
            var changed = typeof(Menu).GetEvent("menuChanged", Internal);
            if (changed != null) changed.AddMethod.Invoke(null, new object[] { (Action)(() => { if (!refreshing) EditorApplication.delayCall += Refresh; }) });
        }

        /// <summary>The items under their names in the current display language.</summary>
        internal static void Refresh()
        {
            if (Add == null || Remove == null) return;
            refreshing = true;
            try
            {
                bool changed = false;
                foreach (var item in Items)
                {
                    var name = Texts.Japanese ? item.Ja : item.En;
                    var other = Texts.Japanese ? item.En : item.Ja;
                    if (IsThere(other)) { Remove.Invoke(null, new object[] { other }); changed = true; }
                    if (!IsThere(name)) { Add.Invoke(null, new object[] { name, "", false, item.Priority, item.Run, item.Enabled }); changed = true; }
                }
                // Only after a change, and inside the guard: redrawing raises menuChanged, which would call this again.
                if (changed) UpdateBar?.Invoke(null, null);
            }
            finally { refreshing = false; }
        }

        /// <summary>Whether this Unity has the internal API the swap uses (else the menu stays English).</summary>
        internal static bool CanSwap => Add != null && Remove != null && Exists != null;

        internal static bool IsThere(string name) => Exists == null || (bool)Exists.Invoke(null, new object[] { name });
    }
}
