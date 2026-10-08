using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UdonIde.Editing;
using UdonBridge;

namespace UdonIde
{
    /// <summary>
    /// A code editor window with tabs. F12 / Shift+F12 / hover / completion ask the check's compilation (every U# source,
    /// with the other tabs' unsaved text). Tabs survive a domain reload.
    /// </summary>
    public partial class CodeEditorWindow : EditorWindow, ISerializationCallbackReceiver
    {
        static Font font;

        [Serializable]
        class Doc
        {
            public string path;
            public string text;          // only when unsaved (kept across domain reloads)
            public bool dirty, readOnly;
            public string newline = "\n";
            public bool bom;             // the file started with a UTF-8 BOM: written back with one
            public bool changedOutside;  // edited here and changed on disk too: saving asks first
            public int line, col;
            public Vector2 scroll;
            public long fileTime;        // the file's write time when last read or written
            [NonSerialized] public TextBuffer buffer;
        }

        [SerializeField] List<Doc> docs = new List<Doc>();
        [SerializeField] int current = -1;
        readonly List<(string path, TextPos pos)> back = new List<(string, TextPos)>();

        CodeView view;
        Label status, panelTitle;
        VisualElement tabs, panel;
        ScrollView list;
        Button problemsButton, referencesButton;
        bool showingReferences;
        List<Problem> problems = new List<Problem>();
        List<SourcePlace> references = new List<SourcePlace>();
        string referencesTitle = "";
        IVisualElementScheduledItem pendingCheck;

        [MenuItem("Tools/Udon IDE (spike)")]
        public static CodeEditorWindow Open() => GetWindow<CodeEditorWindow>("Udon IDE");

        Doc Current => current >= 0 && current < docs.Count ? docs[current] : null;
        string path => Current?.path;
        public CodeView View => view;
        public IReadOnlyList<string> OpenPaths => docs.Select(d => d.path).ToList();
        public string CurrentPath => path;
        public IReadOnlyList<SourcePlace> References => references;

        internal string Describe() => view.Describe() + $"\nroot {rootVisualElement.layout} view {view.layout} tabs {docs.Count}";

        /// <summary><paramref name="remember"/>: Alt+Left comes back here.</summary>
        public void OpenFile(string file, TextPos? at = null, bool remember = false)
        {
            if (remember && Current != null) back.Add((Current.path, view.Buffer.Caret));
            int index = docs.FindIndex(d => d.path == file);
            if (index < 0)
            {
                if (!File.Exists(file)) { SetStatus($"{file} が見つかりません"); return; }
                var doc = new Doc { path = file, readOnly = SymbolNav.IsReadOnlyPath(file) };
                ReadFile(doc);
                docs.Add(doc);
                index = docs.Count - 1;
            }
            Switch(index);
            if (at.HasValue) { view.FocusText(); view.SetCaret(at.Value); }
        }

        void Switch(int index)
        {
            if (Current != null && view != null) Current.scroll = view.Scroll;
            current = index;
            var doc = Current;
            if (doc == null || view == null) return;
            view.SetBuffer(doc.buffer, doc.scroll);
            view.ReadOnly = doc.readOnly;
            view.Problems = new List<Problem>();
            // Drops the previous tab's results still on their way; a worker coming back for more takes this tab.
            lock (pending) { wantedGeneration++; wantedText = doc.buffer.Text; wantedPath = doc.path; wantedHasAsset = UdonSharpCheck.HasProgramAsset(doc.path); }
            problems = new List<Problem>();
            if (list != null) ShowPanel();
            RebuildTabs();
            UpdateTitle();
            ScheduleCheck();
        }

        public void CloseTab(int index)
        {
            var doc = docs[index];
            if (doc.dirty && !EditorUtility.DisplayDialog("保存していない変更", $"{Path.GetFileName(doc.path)} の変更を保存していません。閉じると変更は失われます。", "閉じる", "やめる")) return;
            docs.RemoveAt(index);
            back.RemoveAll(b => b.path == doc.path);
            if (docs.Count == 0) { current = -1; view.SetBuffer(new TextBuffer(), Vector2.zero); RebuildTabs(); UpdateTitle(); return; }
            int next = index < current ? current - 1 : index == current ? Math.Min(index, docs.Count - 1) : current;
            current = -1; // the closed tab keeps nothing
            Switch(next);
        }

        void RebuildTabs()
        {
            if (tabs == null) return;
            tabs.Clear();
            for (int i = 0; i < docs.Count; i++)
            {
                int index = i;
                var doc = docs[i];
                var tab = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, paddingLeft = 10, paddingRight = 4, height = 24,
                    backgroundColor = i == current ? new Color(0.12f, 0.12f, 0.13f) : new Color(0.17f, 0.17f, 0.19f),
                    borderRightWidth = 1, borderRightColor = new Color(0.1f, 0.1f, 0.11f) } };
                var name = new Label((doc.dirty ? "● " : "") + Path.GetFileName(doc.path) + (doc.readOnly ? "（読み取り専用）" : "") + (doc.changedOutside ? "（ほかで変更）" : ""))
                    { style = { color = i == current ? new Color(0.9f, 0.9f, 0.9f) : new Color(0.62f, 0.62f, 0.65f) } };
                name.tooltip = doc.path;
                var close = new Label("×") { style = { marginLeft = 6, paddingLeft = 4, paddingRight = 4, color = new Color(0.6f, 0.6f, 0.63f) } };
                close.RegisterCallback<MouseDownEvent>(e => { CloseTab(index); e.StopPropagation(); });
                tab.RegisterCallback<MouseDownEvent>(_ => Switch(index));
                tab.Add(name);
                tab.Add(close);
                tabs.Add(tab);
            }
        }

        void UpdateTitle()
        {
            var doc = Current;
            titleContent = new GUIContent(doc == null ? "Udon IDE" : (doc.dirty ? "● " : "") + Path.GetFileName(doc.path));
            var unsaved = docs.Where(d => d.dirty).Select(d => Path.GetFileName(d.path)).ToList();
            hasUnsavedChanges = unsaved.Count > 0;
            saveChangesMessage = unsaved.Count == 0 ? "" : "保存していない変更があります: " + string.Join(", ", unsaved);
        }

        public override void SaveChanges()
        {
            var keep = current;
            for (int i = 0; i < docs.Count; i++)
                if (docs[i].dirty && !docs[i].readOnly) { Switch(i); Save(); }
            if (keep >= 0 && keep < docs.Count) Switch(keep);
            // Not base.SaveChanges(): it clears hasUnsavedChanges even when a tab couldn't be saved.
            UpdateTitle();
        }

        // ---- for tests and the dev harnesses ----

        internal void ResetTabs() { docs.Clear(); back.Clear(); current = -1; if (view != null) { view.SetBuffer(new TextBuffer(), Vector2.zero); RebuildTabs(); } }

        internal void Load(string assetPath) { if (view == null) { pendingLoad = assetPath; return; } OpenFile(assetPath); }
        string pendingLoad;

        internal void Select(int from, int to) { view.FocusText(); view.SetCaret(view.Buffer.PosOf(to), view.Buffer.PosOf(from)); }

        internal void SetText(string text)
        {
            var caret = view.Buffer.Caret;
            view.Buffer.Replace(new TextPos(0, 0), view.Buffer.End, text);
            view.Buffer.Caret = view.Buffer.Anchor = view.Buffer.Clamp(caret);
            view.Edited();
        }

        internal void ShowOnly(string text) { view.Buffer.Text = text; view.Scroll = Vector2.zero; view.SetBuffer(view.Buffer, Vector2.zero); pendingCheck?.Pause(); }

        internal void Caret(int at) => view.SetCaret(view.Buffer.PosOf(at));

        // ---- UI ----

        static Font Mono() => font != null ? font : font = AssetDatabase.LoadAssetAtPath<Font>("Packages/dev.haru0416.udon-ide/Editor/Fonts/MPLUS1Code.ttf");

        // Loaded ahead, so the first check and hover after a domain reload don't wait.
        void OnEnable()
        {
            wantsMouseMove = true;
            EditorApplication.delayCall += () =>
            {
                UdonCheck.Warm(out _, out _, out _);
                var core = typeof(UnityEngine.Transform).Assembly.Location;
                System.Threading.Tasks.Task.Run(() => SymbolNav.WarmDocs(core));
            };
        }

        void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.backgroundColor = new Color(0.12f, 0.12f, 0.13f);
            // The bundled font for the whole window: Unity's UI font has no Japanese on some systems (Linux).
            root.style.unityFontDefinition = FontDefinition.FromFont(Mono());

            tabs = new ScrollView(ScrollViewMode.Horizontal) { style = { flexShrink = 0, backgroundColor = new Color(0.15f, 0.15f, 0.17f) } };
            tabs.contentContainer.style.flexDirection = FlexDirection.Row;
            root.Add(tabs);

            status = new Label("") { style = { paddingLeft = 8, paddingTop = 3, paddingBottom = 3, color = new Color(0.62f, 0.62f, 0.65f), flexShrink = 0 } };
            root.Add(status);

            view = new CodeView { Font = Mono() };
            view.Changed += () => { if (Current == null) return; bool was = Current.dirty; Current.dirty = true; if (!was) { RebuildTabs(); UpdateTitle(); } ScheduleCheck(); };
            view.SaveRequested += Save;
            view.DefinitionRequested += GoToDefinition;
            view.ReferencesRequested += FindReferences;
            view.BackRequested += GoBack;
            view.HoverSource = Hover;
            view.CompletionSource = Complete;
            view.CompletionDoc = item => item.Symbol != null && lastCompilation != null ? SymbolNav.Describe(item.Symbol, lastCompilation) : item.Detail;
            root.Add(view);

            panel = new VisualElement { style = { height = 150, flexShrink = 0, borderTopWidth = 1, borderTopColor = new Color(0.25f, 0.25f, 0.27f), backgroundColor = new Color(0.10f, 0.10f, 0.11f) } };
            var header = new VisualElement { style = { flexDirection = FlexDirection.Row, paddingLeft = 4, paddingTop = 2 } };
            problemsButton = new Button(() => { showingReferences = false; ShowPanel(); }) { text = "問題" };
            referencesButton = new Button(() => { showingReferences = true; ShowPanel(); }) { text = "参照" };
            panelTitle = new Label("") { style = { marginLeft = 8, unityTextAlign = TextAnchor.MiddleLeft, color = new Color(0.6f, 0.6f, 0.63f) } };
            header.Add(problemsButton); header.Add(referencesButton); header.Add(panelTitle);
            panel.Add(header);
            list = new ScrollView { style = { flexGrow = 1 } };
            panel.Add(list);
            root.Add(panel);

            foreach (var doc in docs)
            {
                if (doc.text != null) { doc.buffer = new TextBuffer { Text = doc.text }; doc.dirty = true; }
                else if (File.Exists(doc.path)) ReadFile(doc);
                else doc.buffer = new TextBuffer();
                doc.buffer.Caret = doc.buffer.Anchor = doc.buffer.Clamp(new TextPos(doc.line, doc.col));
            }
            docs.RemoveAll(d => d.buffer == null);
            if (Current != null) Switch(current);
            if (pendingLoad != null) { OpenFile(pendingLoad); pendingLoad = null; }
            RebuildTabs();
        }

        void SetStatus(string text) { if (status != null) status.text = text; }

        void ShowPanel()
        {
            list.Clear();
            problemsButton.style.unityFontStyleAndWeight = showingReferences ? FontStyle.Normal : FontStyle.Bold;
            referencesButton.style.unityFontStyleAndWeight = showingReferences ? FontStyle.Bold : FontStyle.Normal;
            problemsButton.text = problems.Count > 0 ? $"問題 {problems.Count}" : "問題";
            referencesButton.text = references.Count > 0 ? $"参照 {references.Count}" : "参照";
            if (!showingReferences)
            {
                panelTitle.text = "";
                foreach (var p in problems)
                {
                    var where = (p.File != null ? p.File + " " : "") + $"{p.Line + 1}:{p.Column + 1}";
                    var color = p.Error ? new Color(0.95f, 0.45f, 0.45f) : new Color(0.95f, 0.8f, 0.4f);
                    var row = Row($"{(p.Error ? "✖" : "▲")}  {where}  [{p.Source}]  {p.Message}", color);
                    var target = p;
                    row.RegisterCallback<MouseDownEvent>(_ => OpenFile(target.File ?? path, new TextPos(target.Line, target.Column), target.File != null));
                    list.Add(row);
                }
                if (problems.Count == 0 && lastFinal) list.Add(Row("問題はありません", new Color(0.5f, 0.8f, 0.5f)));
            }
            else
            {
                panelTitle.text = referencesTitle;
                foreach (var r in references)
                {
                    var row = Row($"{r.Path}:{r.Line + 1}   {r.LineText}", new Color(0.78f, 0.78f, 0.8f));
                    var target = r;
                    row.RegisterCallback<MouseDownEvent>(_ => OpenFile(target.Path, new TextPos(target.Line, target.Column), true));
                    list.Add(row);
                }
            }
        }

        static Label Row(string text, Color color) => new Label(text) { style = { color = color, paddingLeft = 8, whiteSpace = WhiteSpace.Normal } };

        // ---- asking the compilation ----

        /// <summary>For the documentation of completions.</summary>
        Microsoft.CodeAnalysis.CSharp.CSharpCompilation lastCompilation;

        // Longer would freeze the editor while the worker's first parse holds the files.
        const int MainThreadWaitMs = 150;

        bool CompileHere(out Microsoft.CodeAnalysis.CSharp.CSharpCompilation compilation, out SyntaxTree tree)
        {
            PushUnsaved();
            if (!UdonCheck.TryCompile(path, view.Text, MainThreadWaitMs, out compilation, out tree)) return false;
            lastCompilation = compilation;
            return true;
        }

        (SemanticModel model, int offset) ModelAt(TextPos pos)
        {
            if (!CompileHere(out var c, out var t))
                throw new InvalidOperationException("まだファイルを読み込んでいます。少し待ってからもう一度試してください");
            return (c.GetSemanticModel(t), view.Buffer.OffsetOf(pos));
        }

        string Hover(TextPos pos)
        {
            if (path == null) return null;
            try
            {
                var (model, offset) = ModelAt(pos);
                return SymbolNav.Describe(SymbolNav.At(model, offset), lastCompilation);
            }
            catch (Exception e) { SetStatus("ホバーの情報を作れませんでした: " + e.Message); return null; }
        }

        CompletionList Complete(TextPos pos)
        {
            if (path == null || Current.readOnly) return null;
            try
            {
                if (!CompileHere(out var c, out var t)) return null; // the first parse is still running
                return UdonCompletion.At(c, t, view.Buffer.OffsetOf(pos));
            }
            catch (Exception e) { SetStatus("補完を作れませんでした: " + e.Message); return null; }
        }

        void GoToDefinition(TextPos pos)
        {
            if (path == null) return;
            SemanticModel model; int offset;
            try { (model, offset) = ModelAt(pos); }
            catch (Exception e) { SetStatus("定義を探せませんでした: " + e.Message); return; }
            var symbol = SymbolNav.At(model, offset);
            if (symbol == null) { SetStatus("ここには移動先がありません"); return; }
            var def = SymbolNav.Definition(symbol);
            if (def == null) { SetStatus($"{symbol.ToDisplayString()} はソースのない API です({symbol.ContainingAssembly?.Name})。説明はホバーで見られます。"); return; }
            OpenFile(def.Value.Path, new TextPos(def.Value.Line, def.Value.Column), true);
        }

        public void GoBack()
        {
            if (back.Count == 0) return;
            var (file, pos) = back[back.Count - 1];
            back.RemoveAt(back.Count - 1);
            OpenFile(file, pos);
        }

        System.Threading.CancellationTokenSource referencesCancel;

        void FindReferences(TextPos pos)
        {
            if (path == null) return;
            SemanticModel model; int offset;
            try { (model, offset) = ModelAt(pos); }
            catch (Exception e) { SetStatus("参照を探せませんでした: " + e.Message); return; }
            var symbol = SymbolNav.At(model, offset);
            if (symbol == null) { SetStatus("ここには参照を探せるものがありません"); return; }
            referencesCancel?.Cancel();
            referencesCancel = new System.Threading.CancellationTokenSource();
            var cancel = referencesCancel.Token;
            var compilation = lastCompilation;
            referencesTitle = $"「{symbol.Name}」を探しています…";
            references = new List<SourcePlace>();
            showingReferences = true;
            ShowPanel();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var task = System.Threading.Tasks.Task.Run(() => SymbolNav.References(compilation, symbol, cancel), cancel);
            IVisualElementScheduledItem wait = null;
            wait = rootVisualElement.schedule.Execute(() =>
            {
                if (!task.IsCompleted) return;
                wait.Pause();
                if (task.IsCanceled || task.IsFaulted) return;
                references = task.Result;
                referencesTitle = $"「{symbol.Name}」の参照 {references.Count} 件({watch.Elapsed.TotalMilliseconds:F0} ms)";
                LastReferencesMs = watch.Elapsed.TotalMilliseconds;
                ShowPanel();
            }).Every(30);
        }

        internal double LastReferencesMs;
    }
}
