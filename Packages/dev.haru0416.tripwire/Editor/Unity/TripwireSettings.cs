using Tripwire.Core;
using UnityEditor;

namespace Tripwire.Editor
{
    /// <summary>Per-user editor settings (EditorPrefs). The display language follows Unity's editor language until chosen.</summary>
    [InitializeOnLoad]
    internal static class TripwireSettings
    {
        const string LanguageKey = "Tripwire.Language";
        // Both languages in the name: whichever is shown, the way back can be found.
        const string JaMenu = "Tools/Tripwire/表示言語 Language/日本語";
        const string EnMenu = "Tools/Tripwire/表示言語 Language/English";

        static TripwireSettings()
        {
            Texts.Language = (UiLanguage)EditorPrefs.GetInt(LanguageKey, (int)DefaultLanguage());
        }

        /// <summary>
        /// Japanese when the Unity editor is set to Japanese (Preferences > Languages), else English. Unity's setting is
        /// internal (LocalizationDatabase); without it, the system's language decides.
        /// </summary>
        static UiLanguage DefaultLanguage()
        {
            var db = typeof(EditorWindow).Assembly.GetType("UnityEditor.LocalizationDatabase");
            var current = db?.GetProperty("currentEditorLanguage", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.GetValue(null);
            if (current is UnityEngine.SystemLanguage language && language != UnityEngine.SystemLanguage.Unknown)
                return language == UnityEngine.SystemLanguage.Japanese ? UiLanguage.Japanese : UiLanguage.English;
            return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ja" ? UiLanguage.Japanese : UiLanguage.English;
        }

        const string DetailedKey = "Tripwire.Detailed";

        /// <summary>
        /// "くわしく" (detailed) shows every event and action kind and the Udon names; "かんたん" (simple, the default)
        /// keeps the pickers to what most gimmicks use. Triggers keep working either way; only the pickers change.
        /// </summary>
        public static bool Detailed
        {
            get => EditorPrefs.GetBool(DetailedKey, false);
            set { EditorPrefs.SetBool(DetailedKey, value); InternalEditorRepaint(); }
        }

        public static void SetLanguage(UiLanguage language) => Set(language);

        static void Set(UiLanguage language)
        {
            Texts.Language = language;
            EditorPrefs.SetInt(LanguageKey, (int)language);
            TripwireMenus.Refresh();
            InternalEditorRepaint();
        }

        static void InternalEditorRepaint() => UnityEditorInternal.InternalEditorUtility.RepaintAllViews();

        [MenuItem(JaMenu, false, 1040)] static void UseJapanese() => Set(UiLanguage.Japanese);
        [MenuItem(EnMenu, false, 1041)] static void UseEnglish() => Set(UiLanguage.English);

        [MenuItem(JaMenu, true)]
        static bool CheckJapanese() { Menu.SetChecked(JaMenu, Texts.Japanese); return true; }

        [MenuItem(EnMenu, true)]
        static bool CheckEnglish() { Menu.SetChecked(EnMenu, !Texts.Japanese); return true; }
    }
}
