using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Tripwire.Core
{
    /// <summary>
    /// What "Export for distribution" writes: scenes and prefabs without the Tripwire component (the generated
    /// UdonBehaviour stays and needs only UdonSharp), packed as a .unitypackage. Works on Unity's text YAML.
    /// </summary>
    public static class Distribution
    {
        static readonly Regex Header = new Regex(@"^--- !u!(\d+) &(-?\d+)( stripped)?", RegexOptions.Compiled);
        static readonly Regex Script = new Regex(@"^  m_Script: \{fileID: 11500000, guid: ([0-9a-f]{32}), type: 3\}", RegexOptions.Compiled | RegexOptions.Multiline);
        static readonly Regex ComponentEntry = new Regex(@"^\s*- component: \{fileID: (-?\d+)\}\s*$", RegexOptions.Compiled);
        static readonly Regex SourceRef = new Regex(@"^\s*- (?:target: )?\{fileID: (-?\d+), guid: ([0-9a-f]{32}), type: \d+\}", RegexOptions.Compiled);
        static readonly Regex AddedObject = new Regex(@"^\s*addedObject: \{fileID: (-?\d+)\}", RegexOptions.Compiled);
        static readonly Regex EmptyKey = new Regex(@"^(\s*)([\w]+):\s*$", RegexOptions.Compiled);

        /// <summary>Whether the file is Unity's text YAML (a binary one can't be changed here).</summary>
        public static bool IsText(string content) => content.StartsWith("%YAML", StringComparison.Ordinal);

        sealed class Doc { public int ClassId; public long Id; public List<string> Lines = new List<string>(); }

        static List<Doc> Split(string yaml, out List<string> preamble)
        {
            preamble = new List<string>();
            var docs = new List<Doc>();
            Doc current = null;
            foreach (var line in yaml.Replace("\r\n", "\n").Split('\n'))
            {
                var m = Header.Match(line);
                if (m.Success)
                {
                    current = new Doc { ClassId = int.Parse(m.Groups[1].Value), Id = long.Parse(m.Groups[2].Value) };
                    docs.Add(current);
                }
                (current?.Lines ?? preamble).Add(line);
            }
            return docs;
        }

        /// <summary>The file ids of the components (also stripped copies from a nested prefab) whose script is scriptGuid.</summary>
        public static HashSet<long> ComponentIds(string yaml, string scriptGuid)
        {
            var ids = new HashSet<long>();
            foreach (var doc in Split(yaml, out _))
                if (doc.ClassId == 114 && doc.Lines.Any(l => { var m = Script.Match(l); return m.Success && m.Groups[1].Value == scriptGuid; }))
                    ids.Add(doc.Id);
            return ids;
        }

        /// <summary>
        /// The file without the components whose script is scriptGuid: their documents, their entries in their objects'
        /// component lists, and what prefab instances add to them or change on them. sources: for each other file being
        /// changed the same way (by its guid), the ids of its removed components, which this file's prefab instances may
        /// still point at.
        /// </summary>
        public static string Strip(string yaml, string scriptGuid, IDictionary<string, HashSet<long>> sources, out int removed)
        {
            var ids = ComponentIds(yaml, scriptGuid);
            removed = ids.Count;
            var docs = Split(yaml, out var preamble);
            var output = new List<string>(preamble);
            foreach (var doc in docs)
                if (!ids.Contains(doc.Id)) output.AddRange(Kept(doc, ids, sources));
            // Written back with the file's own line ends (a Windows checkout may have CRLF).
            var newline = yaml.Contains("\r\n") ? "\r\n" : "\n";
            var text = string.Join(newline, output);
            // The file's last newline belongs to its last document, which may be gone.
            return yaml.EndsWith("\n", StringComparison.Ordinal) && !text.EndsWith("\n", StringComparison.Ordinal) ? text + newline : text;
        }

        static int Indent(string line) => line.Length - line.TrimStart(' ').Length;
        static bool Blank(string line) => line.Trim().Length == 0;

        /// <summary>
        /// Whether line j still belongs to something at indent: deeper lines do, and so do blank lines when a deeper line
        /// follows them (Unity writes a newline inside a quoted string as an unindented blank line).
        /// </summary>
        static bool Under(List<string> lines, int j, int indent)
        {
            if (!Blank(lines[j])) return Indent(lines[j]) > indent;
            while (j < lines.Count && Blank(lines[j])) j++;
            return j < lines.Count && Indent(lines[j]) > indent && !Header.IsMatch(lines[j]);
        }
        static bool IsItem(string line) => line.TrimStart(' ').StartsWith("- ", StringComparison.Ordinal);

        /// <summary>
        /// A document without the entries about removed components: in a component list, and in a prefab instance's
        /// modifications, removals and additions. A list that loses every entry is written "key: []", as Unity does.
        /// </summary>
        static IEnumerable<string> Kept(Doc doc, HashSet<long> ids, IDictionary<string, HashSet<long>> sources)
        {
            var lines = doc.Lines;
            var keep = Enumerable.Repeat(true, lines.Count).ToArray();
            for (int i = 0; i < lines.Count; i++)
            {
                if (!IsItem(lines[i])) continue;
                // One entry: the "- " line and the deeper lines under it.
                int indent = Indent(lines[i]), end = i + 1;
                while (end < lines.Count && Under(lines, end, indent)) end++;
                if (Drops(lines, i, end, doc.ClassId == 1001, ids, sources))
                    for (int k = i; k < end; k++) keep[k] = false;
                i = end - 1;
            }
            for (int i = 0; i < lines.Count; i++)
            {
                if (!keep[i]) continue;
                var key = EmptyKey.Match(lines[i]);
                if (key.Success && i + 1 < lines.Count && IsItem(lines[i + 1]) && Indent(lines[i + 1]) == key.Groups[1].Value.Length)
                {
                    // A list (Unity writes its "- " entries at the key's indent): still has an entry?
                    int indent = key.Groups[1].Value.Length;
                    bool any = false;
                    for (int j = i + 1; j < lines.Count && (Under(lines, j, indent) || Indent(lines[j]) == indent && IsItem(lines[j])); j++)
                        if (keep[j] && !Blank(lines[j]) && Indent(lines[j]) == indent) { any = true; break; }
                    if (!any) { yield return lines[i].TrimEnd() + " []"; continue; }
                }
                yield return lines[i];
            }
        }

        static bool Drops(List<string> lines, int start, int end, bool prefabInstance, HashSet<long> ids, IDictionary<string, HashSet<long>> sources)
        {
            var first = lines[start];
            var component = ComponentEntry.Match(first);
            if (component.Success) return ids.Contains(long.Parse(component.Groups[1].Value));
            if (!prefabInstance) return false;
            var source = SourceRef.Match(first);
            if (source.Success && sources != null && sources.TryGetValue(source.Groups[2].Value, out var there) && there.Contains(long.Parse(source.Groups[1].Value))) return true;
            for (int k = start; k < end; k++)
            {
                var added = AddedObject.Match(lines[k]);
                if (added.Success && ids.Contains(long.Parse(added.Groups[1].Value))) return true;
            }
            return false;
        }

        /// <summary>One asset in a .unitypackage: its guid, its path in the project, its .meta, and its content (none for a folder).</summary>
        public sealed class Entry
        {
            public string Guid;
            public string PathName;
            public byte[] Meta;
            /// <summary>Changed content (a stripped scene or prefab), or null to copy <see cref="File"/>.</summary>
            public byte[] Content;
            public string File;
        }

        /// <summary>Writes a .unitypackage (a gzipped tar of guid/pathname, guid/asset.meta, guid/asset), as Unity exports one.</summary>
        public static void WritePackage(Stream output, IEnumerable<Entry> entries)
        {
            using (var gz = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
            {
                var time = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                TarHeader(gz, "./", 0, '5', time);
                foreach (var e in entries)
                {
                    var dir = "./" + e.Guid + "/";
                    TarHeader(gz, dir, 0, '5', time);
                    TarFile(gz, dir + "pathname", Encoding.UTF8.GetBytes(e.PathName), time);
                    TarFile(gz, dir + "asset.meta", e.Meta, time);
                    if (e.Content != null) TarFile(gz, dir + "asset", e.Content, time);
                    else if (e.File != null)
                        using (var f = System.IO.File.OpenRead(e.File))
                        {
                            TarHeader(gz, dir + "asset", f.Length, '0', time);
                            f.CopyTo(gz);
                            Pad(gz, f.Length);
                        }
                }
                gz.Write(new byte[1024], 0, 1024); // two empty blocks end the archive
            }
        }

        static void TarFile(Stream s, string name, byte[] data, long time)
        {
            TarHeader(s, name, data.Length, '0', time);
            s.Write(data, 0, data.Length);
            Pad(s, data.Length);
        }

        static void Pad(Stream s, long length)
        {
            int rest = (int)(length % 512);
            if (rest != 0) s.Write(new byte[512 - rest], 0, 512 - rest);
        }

        static void TarHeader(Stream s, string name, long size, char type, long time)
        {
            var h = new byte[512];
            void Put(int at, int length, string text) { var b = Encoding.ASCII.GetBytes(text); Array.Copy(b, 0, h, at, Math.Min(b.Length, length)); }
            string Octal(long v, int digits) => Convert.ToString(v, 8).PadLeft(digits, '0');
            Put(0, 100, name);
            Put(100, 8, (type == '5' ? "0000755" : "0000644") + "\0");
            Put(108, 8, "0000000\0");
            Put(116, 8, "0000000\0");
            Put(124, 12, Octal(size, 11) + "\0");
            Put(136, 12, Octal(time, 11) + "\0");
            Put(148, 8, "        ");
            h[156] = (byte)type;
            Put(257, 6, "ustar\0");
            Put(263, 2, "00");
            int sum = h.Sum(b => b);
            Put(148, 8, Octal(sum, 6) + "\0 ");
            s.Write(h, 0, 512);
        }
    }
}
