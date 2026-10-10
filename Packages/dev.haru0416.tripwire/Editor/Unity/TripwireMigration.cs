using Tripwire.Core;
using UnityEditor;

namespace Tripwire.Editor
{
    /// <summary>
    /// Brings saved trigger data up to <see cref="TripwireTrigger.CurrentDataVersion"/>. A format change adds a step
    /// here (and raises the version); renamed ids go into the catalogs' Renamed tables instead.
    /// </summary>
    internal static class TripwireMigration
    {
        /// <summary>Saved by a newer Tripwire: this one must neither edit nor apply it, or the settings it doesn't know are lost.</summary>
        public static bool IsNewer(TripwireTrigger t) => t != null && t.dataVersion > TripwireTrigger.CurrentDataVersion;

        public static string NewerMessage => Texts.T(
            "This trigger was saved by a newer version of Tripwire. Update Tripwire to edit or apply it: this version would drop the settings it doesn't know.",
            "このトリガーは新しい版の Tripwire で保存されています。編集や反映の前に Tripwire を更新してください（この版では、知らない設定が失われます）。");

        /// <summary>Upgrades older data in place (recorded for undo). True when something changed.</summary>
        public static bool Upgrade(TripwireTrigger t)
        {
            // Versions 0 and 1 are version 2 without its new settings (which start empty), so nothing is rewritten
            // (opening a scene doesn't make it unsaved); the number is recorded with the next edit (MarkCurrent).
            // An older Tripwire refuses version 2, which would drop those settings (IsNewer).
            if (t == null || t.dataVersion >= TripwireTrigger.CurrentDataVersion || TripwireTrigger.CurrentDataVersion == 2) return false;
            Undo.RecordObject(t, "Upgrade Tripwire Trigger");
            // Steps for later formats go here, oldest first: if (t.dataVersion < 3) { ...; }
            t.dataVersion = TripwireTrigger.CurrentDataVersion;
            EditorUtility.SetDirty(t);
            return true;
        }

        /// <summary>An edited trigger is saved in the current format.</summary>
        public static void MarkCurrent(TripwireTrigger t)
        {
            if (t != null && t.dataVersion < TripwireTrigger.CurrentDataVersion) t.dataVersion = TripwireTrigger.CurrentDataVersion;
        }
    }
}
