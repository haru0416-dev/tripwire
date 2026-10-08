using NUnit.Framework;
using Tripwire.Core;
using Tripwire.Editor;
using UnityEditor;

namespace Tripwire.Tests
{
    /// <summary>Tools > Tripwire shows its items in the display language only.</summary>
    public class MenuLanguageTests
    {
        const string JaApply = "Tools/Tripwire/すべて反映";

        [Test]
        public void ToolsMenuFollowsTheDisplayLanguage()
        {
            if (!TripwireMenus.CanSwap) Assert.Ignore("this Unity has no Menu.AddMenuItem: the menu stays English");
            var saved = EditorPrefs.HasKey("Tripwire.Language");
            var before = Texts.Language;
            try
            {
                TripwireSettings.SetLanguage(UiLanguage.Japanese);
                Assert.IsTrue(TripwireMenus.IsThere(JaApply));
                Assert.IsFalse(TripwireMenus.IsThere(TripwireMenus.ApplyAll), "no English item next to the Japanese one");
                Assert.IsTrue(TripwireMenus.IsThere("Tools/Tripwire/表示言語 Language/English"), "the way back is always there");

                TripwireSettings.SetLanguage(UiLanguage.English);
                Assert.IsTrue(TripwireMenus.IsThere(TripwireMenus.ApplyAll));
                Assert.IsTrue(TripwireMenus.IsThere(TripwireMenus.List));
                Assert.IsTrue(TripwireMenus.IsThere(TripwireMenus.DeleteUnused));
                Assert.IsFalse(TripwireMenus.IsThere(JaApply));
            }
            finally
            {
                TripwireSettings.SetLanguage(before);
                if (!saved) EditorPrefs.DeleteKey("Tripwire.Language");
            }
        }
    }
}
