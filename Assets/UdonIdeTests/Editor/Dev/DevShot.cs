using System.IO;
using UnityEditor;
using System.Linq;
using UnityEngine;

namespace UdonIde.Dev
{
    // Dev helper: open the editor window under Xvfb with a sample script; an outside script screenshots it
    // (handshake: Logs/ide-ready / Logs/ide-done).
    public static class DevShot
    {
        static int frames, phase = -1;
        static string log = "";
        static double setMs, typeMs;
        static bool sizesDone, sceneSet;
        static int scene, sceneAt;
        static string Text(string replaceFrom, string replaceTo) => Door().Replace(replaceFrom, replaceTo);
        const string NavText = "using UdonSharp;\nusing UnityEngine;\nusing VRC.SDKBase;\nusing ToyMedia;\n\n// 扉とプレイヤーをつなぐ\npublic class Door : UdonSharpBehaviour\n{\n    public Helper helper;\n    public ToyPlayer toy;\n    public VRCUrl link;\n    public GameObject panel;\n\n    public override void Interact()\n    {\n        helper.Ping();\n        toy.Play(link);\n        helper.SendCustomEvent(\"Pnig\");\n        \n        panel.SetActive(!toy.IsPlaying());\n    }\n}\n";

        static void Key(string name)
        {
            // As Unity sends a key: the key code alone, then (for a printable key) the character alone.
            var e = Event.KeyboardEvent(name);
            var c = e.character;
            window.SendEvent(new Event(e) { character = '\0' });
            if (c != 0 && !char.IsControl(c)) window.SendEvent(new Event(e) { keyCode = KeyCode.None, character = c });
        }

        static void Type(string text) { foreach (var c in text) window.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.None, character = c }); }

        static UdonIde.Editing.TextPos PosOf(string marker, int into = 0)
        {
            var t = window.View.Text;
            return window.View.Buffer.PosOf(t.IndexOf(marker) + into);
        }

        static void Note(string line) => File.AppendAllText("Logs/ide-nav.txt", line + "\n");

        static readonly System.Action[] Scenes =
        {
            () =>
            {
                // Typing "toy." opens the list with the third-party player's own members first.
                window.SetText(NavText);
                window.View.FocusText();
                window.View.SetCaret(PosOf("        \n        panel", 8));
                Type("toy.");
                Note("after typing toy.: open " + window.View.CompletionOpen + ", first: " + string.Join(", ", window.View.CompletionItems.Take(6).Select(i => i.Name + (i.Exposed ? "" : "(dim)"))));
            },
            () =>
            {
                // Narrow it, accept with Enter; then hover over Play.
                Type("st");
                Note("after typing st: " + string.Join(", ", window.View.CompletionItems.Take(4).Select(i => i.Name)));
                Key("return");
                Note("accepted line: [" + window.View.Buffer.Lines[window.View.Buffer.Caret.Line].Trim() + "]");
                window.CheckNow();
                var hover = window.View.HoverAt(PosOf("Play(link)", 1));
                Note("hover on Play: " + hover?.Replace("\n", " / "));
            },
            () =>
            {
                // F12 on Ping: Helper.cs opens in a new tab at the method.
                window.View.FocusText();
                window.View.SetCaret(PosOf("Ping();", 1));
                Key("f12");
                Note("after F12: current " + window.CurrentPath + " caret " + window.View.Buffer.Caret + ", tabs " + string.Join(", ", window.OpenPaths));
            },
            () =>
            {
                // Shift+F12 on Ping (in Helper.cs): every use, the SendCustomEvent string included.
                window.View.FocusText();
                Key("#f12");
                sceneWaitsForReferences = true;
            },
            () =>
            {
                Note("references: " + window.References.Count + " in " + window.LastReferencesMs.ToString("F0") + " ms: " + string.Join(" | ", window.References.Select(r => r.Path + ":" + (r.Line + 1))));
                // Alt+Left: back to Door, where the misspelt event name is underlined.
                Key("&left");
                Note("after Alt+Left: current " + window.CurrentPath + " caret " + window.View.Buffer.Caret);
                window.CheckNow();
            },
        };
        static bool sceneWaitsForReferences;
        static readonly int[] Sizes = { 80, 280, 480, 980, 1980, 4980 };
        static string Door() => File.ReadAllText("Assets/UdonIdeTests/Fixtures/Door.cs");
        static readonly (string name, System.Func<string> text)[] Steps =
        {
            ("first", Door),
            ("broken", () => Door().Replace("open = !open;", "open = !opne;\n        int x = \"text\";")),
            ("try/catch", () => Door().Replace("RequestSerialization();", "RequestSerialization();\n        try { open = true; } catch { open = false; }\n        var t = transform.position;")),
            ("2000 methods", () => Door().Replace("    public GameObject panel;", "    public GameObject panel;\n" + string.Concat(Enumerable.Range(0, 2000).Select(i => $"    public int M{i}(int x) {{ return x + {i}; }}\n")))),
            ("3000-term sum", () => Door().Replace("        RequestSerialization();", "        RequestSerialization();\n        int sum = 0" + string.Concat(Enumerable.Repeat(" + 1", 3000)) + "; Debug.Log(sum);")),
        };
        static CodeEditorWindow window;

        public static void Run()
        {
            foreach (var f in Directory.GetFiles("Logs", "ide-*")) File.Delete(f);
            window = CodeEditorWindow.Open();
            window.position = new Rect(20, 40, 900, 700);
            window.ResetTabs();
            window.Load("Assets/UdonIdeTests/Fixtures/Door.cs");
            EditorApplication.update += Tick;
        }

        static readonly System.Diagnostics.Stopwatch frameClock = System.Diagnostics.Stopwatch.StartNew();
        static double lastFrame, worstFrame;

        static void Tick()
        {
            var now = frameClock.Elapsed.TotalMilliseconds;
            if (frames > 0) worstFrame = System.Math.Max(worstFrame, now - lastFrame);
            lastFrame = now;
            frames++;
            window.Repaint();
            // Drawing alone: texts of growing length shown without a check, then one character typed into each.
            if (!sizesDone && frames >= 20 && frames < 20 + Sizes.Length * 12)
            {
                int k = (frames - 20) / 12, f = (frames - 20) % 12;
                string Text(int n) => Door().Replace("    public GameObject panel;", "    public GameObject panel;\n" + string.Concat(Enumerable.Range(0, n).Select(i => $"    public int M{i}(int x) {{ return x + {i}; }}\n")));
                if (f == 0) { window.ShowOnly(Text(Sizes[k])); window.View.FocusText(); window.View.SetCaret(new UdonIde.Editing.TextPos(Sizes[k] / 2, 12)); worstFrame = 0; }
                if (f == 5)
                {
                    log += $"{Sizes[k] + 20} lines: show {worstFrame:F0} ms"; worstFrame = 0;
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    UdonIde.Editing.KeyMap.TypeChar(window.View.Buffer, 'x'); // a keystroke, in the middle of the file
                    window.View.Edited();
                    typeMs = sw.Elapsed.TotalMilliseconds;
                }
                if (f == 10) { log += $", one character typed: handling {typeMs:F1} ms, slowest frame {worstFrame:F0} ms\n"; }
                return;
            }
            if (!sizesDone && frames == 20 + Sizes.Length * 12) { sizesDone = true; window.ShowOnly(Door()); worstFrame = 0; window.CheckNow(); phase = 0; frames = 61; }
            if (phase >= 0 && phase < Steps.Length && window.CheckDone)
            {
                log += $"{Steps[phase].name}: redraw {setMs:F0} ms, check call {window.LastCallMs:F1} ms (main thread); result after {window.LastCheckMs:F0} ms, problems {window.LastProblemCount}, slowest frame while waiting {worstFrame:F0} ms\n";
                worstFrame = 0;
                phase++;
                if (phase < Steps.Length)
                {
                    var t = Steps[phase].text();
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    window.SetText(t); // highlighting and line numbers: main thread
                    setMs = sw.Elapsed.TotalMilliseconds;
                    window.CheckNow();
                }
                else { File.WriteAllText("Logs/ide-timing.txt", log); window.SetText(Steps[2].text()); window.CheckNow(); frames = 95; phase = 99; }
            }
            if (phase < 99) { if (frames > 3000) { File.WriteAllText("Logs/ide-timing.txt", log + "TIMEOUT at " + phase); phase = 99; frames = 95; } else return; }
            if (frames == 100)
            {
                // Select "閉じた板" (Japanese) on the panel line: the highlight must sit exactly on those glyphs.
                var text = window.View.Text;
                int at = text.IndexOf("閉じた板");
                window.Select(at, at + "閉じた板".Length);
            }
            // Real key events through the window (UI Toolkit -> IMGUI -> KeyMap), not buffer calls.
            if (frames == 104)
            {
                window.View.FocusText();
                window.View.SetCaret(new UdonIde.Editing.TextPos(2, 18)); // end of "using VRC.SDKBase;"
            }
            if (frames == 106)
            {
                var before = window.View.Text;
                foreach (var e in new[] { Event.KeyboardEvent("return"), Event.KeyboardEvent("u"), Event.KeyboardEvent("s"), Event.KeyboardEvent("backspace"), Event.KeyboardEvent("x") })
                {
                    // As Unity sends a printable key: the key code alone, then the character alone.
                    var c = e.character;
                    var keyOnly = new Event(e) { character = '\0' };
                    window.SendEvent(keyOnly);
                    if (c != 0 && !char.IsControl(c)) window.SendEvent(new Event(e) { keyCode = KeyCode.None, character = c });
                }
                var lines = window.View.Text.Split('\n');
                File.WriteAllText("Logs/ide-keys.txt", $"line 4 after keys: [{lines[3]}] caret {window.View.Buffer.Caret}\n");
                int undos = 0;
                while (window.View.Text != before && undos < 10) { window.SendEvent(new Event(Event.KeyboardEvent("^z")) { character = '\0' }); undos++; }
                File.AppendAllText("Logs/ide-keys.txt", $"ctrl+z back to start: {window.View.Text == before} after {undos} presses\n");
            }
            if (frames == 110) File.WriteAllText("Logs/ide-layout.txt", window.Describe());
            // Scenes for screenshots: set one up, wait for its check, signal Logs/ide-ready-N, wait for Logs/ide-done-N.
            if (frames >= 120 && scene < Scenes.Length)
            {
                if (!sceneSet) { Scenes[scene](); sceneSet = true; sceneAt = frames; }
                else if (frames - sceneAt > 40 && window.CheckDone && (!sceneWaitsForReferences || window.References.Count > 0) && !File.Exists($"Logs/ide-ready-{scene}")) { sceneWaitsForReferences = false; File.WriteAllText($"Logs/ide-ready-{scene}", "ok"); }
                else if (File.Exists($"Logs/ide-done-{scene}")) { scene++; sceneSet = false; }
            }
            if (scene >= Scenes.Length) { EditorApplication.update -= Tick; EditorApplication.Exit(0); }
        }
    }
}
