using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace UdonIde.Tests
{
    /// <summary>The editor window's files: encoding and BOM kept, non-UTF-8 opened read-only, unsaved tabs reported to Unity.</summary>
    public class WindowFileTests
    {
        const string Dir = "Assets/UdonIdeTests/Temp";
        CodeEditorWindow window;

        [TearDown]
        public void Clean()
        {
            if (window != null) { window.ResetTabs(); window.Close(); }
            if (Directory.Exists(Dir)) { Directory.Delete(Dir, true); File.Delete(Dir + ".meta"); }
        }

        IEnumerator OpenWindow()
        {
            window = CodeEditorWindow.Open();
            window.ResetTabs();
            for (int i = 0; i < 5 && window.View == null; i++) yield return null;
            Assert.IsNotNull(window.View, "the window built its view");
        }

        [UnityTest]
        public IEnumerator SavingKeepsTheBomAndLineEndings()
        {
            Directory.CreateDirectory(Dir);
            var path = Dir + "/Bom.cs";
            File.WriteAllBytes(path, new byte[] { 0xEF, 0xBB, 0xBF }.Concat(System.Text.Encoding.UTF8.GetBytes("// あ\r\nclass A {}\r\n")).ToArray());
            yield return OpenWindow();
            window.OpenFile(path);
            Assert.IsFalse(window.hasUnsavedChanges);
            window.SetText(window.View.Text.Replace("class A", "class B"));
            Assert.IsTrue(window.hasUnsavedChanges, "Unity is told about the unsaved tab (it asks before closing)");
            window.Save();
            var bytes = File.ReadAllBytes(path);
            Assert.AreEqual(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray(), "the BOM stays");
            Assert.AreEqual("// あ\r\nclass B {}\r\n", System.Text.Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3), "CRLF and the Japanese stay");
            Assert.IsFalse(window.hasUnsavedChanges);
        }

        [UnityTest]
        public IEnumerator NonUtf8FilesOpenReadOnly()
        {
            Directory.CreateDirectory(Dir);
            var path = Dir + "/Sjis.cs";
            var sjis = new byte[] { 0x2F, 0x2F, 0x20, 0x82, 0xA0, 0x0A }; // "// あ" in Shift-JIS
            File.WriteAllBytes(path, sjis);
            LogAssert.ignoreFailingMessages = true;
            yield return OpenWindow();
            window.OpenFile(path);
            Assert.IsTrue(window.View.ReadOnly, "opened read-only");
            window.Save();
            Assert.AreEqual(sjis, File.ReadAllBytes(path), "the file is untouched");
        }

        [UnityTest]
        public IEnumerator AFileThatTurnsNonUtf8IsReadOnlyAndKeepsTheWindowOpen()
        {
            Directory.CreateDirectory(Dir);
            var path = Dir + "/Turns.cs";
            File.WriteAllText(path, "// a\n");
            LogAssert.ignoreFailingMessages = true;
            yield return OpenWindow();
            window.OpenFile(path);
            Assert.IsFalse(window.View.ReadOnly);
            // Another tool rewrites it in Shift-JIS; coming back to the window reloads the unedited tab.
            File.WriteAllBytes(path, new byte[] { 0x2F, 0x2F, 0x20, 0x82, 0xA0, 0x0A });
            File.SetLastWriteTimeUtc(path, System.DateTime.UtcNow.AddSeconds(5));
            typeof(CodeEditorWindow).GetMethod("OnFocus", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(window, null);
            Assert.IsTrue(window.View.ReadOnly, "the view follows the reloaded tab");

            // An unsaved tab that can't be written (here forced past the view): Unity's close prompt "Save" must not
            // report it saved, or the window closes and the edit is lost.
            window.SetText("// b\n");
            Assert.IsTrue(window.hasUnsavedChanges);
            window.SaveChanges();
            Assert.IsTrue(window.hasUnsavedChanges, "still unsaved after Save in the close prompt");
        }
    }
}
