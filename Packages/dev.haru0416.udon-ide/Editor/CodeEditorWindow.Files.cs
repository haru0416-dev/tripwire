using System.IO;
using UnityEditor;
using UnityEngine;
using UdonIde.Editing;

namespace UdonIde
{
    public partial class CodeEditorWindow
    {
        static readonly System.Text.UTF8Encoding StrictUtf8 = new System.Text.UTF8Encoding(false, true);

        static void ReadFile(Doc doc)
        {
            // Only UTF-8 is written back unchanged: anything else (Shift-JIS) opens read-only.
            var bytes = File.ReadAllBytes(doc.path);
            doc.bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            string text;
            try { text = StrictUtf8.GetString(bytes, doc.bom ? 3 : 0, bytes.Length - (doc.bom ? 3 : 0)); }
            catch (System.Text.DecoderFallbackException)
            {
                text = System.Text.Encoding.UTF8.GetString(bytes);
                doc.readOnly = true;
                Debug.LogWarning("[Udon IDE] " + doc.path + " is not UTF-8: opened read-only so saving can't change its characters.");
            }
            doc.newline = text.Contains("\r\n") ? "\r\n" : "\n"; // written back as it was
            doc.buffer = new TextBuffer { Text = text };
            doc.dirty = false;
            doc.changedOutside = false;
            doc.fileTime = File.GetLastWriteTimeUtc(doc.path).Ticks;
        }

        public void Save()
        {
            var doc = Current;
            if (doc == null || doc.readOnly) return;
            if (File.Exists(doc.path) && File.GetLastWriteTimeUtc(doc.path).Ticks != doc.fileTime
                && !EditorUtility.DisplayDialog("ほかで変更されたファイル", Path.GetFileName(doc.path) + " は、開いたあとでほかで変更されています。保存すると、その変更は失われます。", "上書きする", "やめる"))
                return;
            var text = view.Text;
            File.WriteAllText(doc.path, doc.newline == "\n" ? text : text.Replace("\n", doc.newline), new System.Text.UTF8Encoding(doc.bom));
            doc.dirty = false;
            doc.changedOutside = false;
            doc.fileTime = File.GetLastWriteTimeUtc(doc.path).Ticks;
            RebuildTabs();
            UpdateTitle();
            if (!EditorApplication.isPlaying) AssetDatabase.ImportAsset(doc.path);
        }

        // Unchanged tabs reload what changed on disk; edited ones are marked, and saving asks.
        void OnFocus()
        {
            if (view == null) return;
            foreach (var doc in docs)
            {
                if (!File.Exists(doc.path) || doc.buffer == null) continue;
                long time = File.GetLastWriteTimeUtc(doc.path).Ticks;
                if (time == doc.fileTime) continue;
                if (doc.dirty) { if (!doc.changedOutside) { doc.changedOutside = true; RebuildTabs(); } continue; }
                var caret = doc.buffer.Caret;
                ReadFile(doc);
                doc.buffer.Caret = doc.buffer.Anchor = doc.buffer.Clamp(caret);
                if (doc == Current) { view.SetBuffer(doc.buffer, view.Scroll); view.ReadOnly = doc.readOnly; ScheduleCheck(); }
                RebuildTabs();
            }
        }

        public void OnBeforeSerialize()
        {
            foreach (var doc in docs)
            {
                if (doc.buffer == null) continue;
                doc.text = doc.dirty ? doc.buffer.Text : null;
                doc.line = doc.buffer.Caret.Line; doc.col = doc.buffer.Caret.Col;
            }
            if (Current != null && view != null) Current.scroll = view.Scroll;
        }

        public void OnAfterDeserialize() { }
    }
}
