using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tripwire.Core;
using UnityEditor;
using UnityEngine;

namespace Tripwire.Editor
{
    /// <summary>
    /// Export for distribution, from the Project selection: what goes in, what loses Tripwire, which packages the person
    /// receiving it needs, and what has to be fixed first; then the .unitypackage. Tools > Tripwire, or a right click in
    /// the Project window.
    /// </summary>
    internal sealed class TripwireExportWindow : EditorWindow
    {
        TripwireExport.Plan plan;
        List<string> selection = new List<string>();
        Vector2 scroll;
        bool showFiles, keepTripwire;

        static string T(string en, string ja) => Texts.T(en, ja);

        [MenuItem(TripwireMenus.Export, true)] [MenuItem(TripwireMenus.ExportFromAssets, true)] static bool CanOpen() => TripwireMenus.NotPlaying();
        [MenuItem(TripwireMenus.Export, false, 1010)]
        [MenuItem(TripwireMenus.ExportFromAssets, false, 1100)]
        internal static void Open()
        {
            var w = GetWindow<TripwireExportWindow>();
            w.Refresh();
            w.Show();
        }

        void OnEnable() => titleContent = new GUIContent(T("Export for Distribution", "配布用に書き出す"), TripwireTriggerEditor.LogoMark);

        /// <summary>Takes the Project selection now (on opening, and with "Use the current selection").</summary>
        void Refresh()
        {
            selection = Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).Where(p => !string.IsNullOrEmpty(p)).ToList();
            plan = TripwireExport.MakePlan(selection, keepTripwire);
        }

        void OnGUI()
        {
            titleContent.text = T("Export for Distribution", "配布用に書き出す");
            if (plan == null) plan = TripwireExport.MakePlan(selection, keepTripwire); // after a script reload: the same selection
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField(T("Makes a .unitypackage of the selected prefabs, scenes or folders that works without Tripwire: the Tripwire components come out, and the generated scripts (which need only UdonSharp) go in. Your own prefabs and scenes don't change.",
                                         "選んだプレハブ・シーン・フォルダを、Tripwire を入れていない人にも渡せる .unitypackage にします。Tripwire のコンポーネントは外し、生成したスクリプト（UdonSharp だけで動きます）を入れます。元のプレハブやシーンは変わりません。"), EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(4);
            bool keep = EditorGUILayout.ToggleLeft(new GUIContent(T("Keep the cards (an editable version, for people who have Tripwire)", "カードを残す（Tripwire を持っている人向けの、直せる版）"),
                T("Off: works without Tripwire. On: the person receiving it needs Tripwire and can edit the cards.", "オフ: Tripwire なしで動く版。オン: 受け取る人に Tripwire が要り、カードとして直せる版。")), keepTripwire);
            if (keep != keepTripwire) { keepTripwire = keep; plan = TripwireExport.MakePlan(selection, keepTripwire); }
            EditorGUILayout.Space(6);

            foreach (var problem in plan.Problems) EditorGUILayout.HelpBox(problem, MessageType.Error);
            if (plan.Unsaved.Count > 0 && GUILayout.Button(T("Save them and check again", "保存して確かめ直す")))
            {
                TripwireExport.Save(plan.Unsaved);
                plan = TripwireExport.MakePlan(selection, keepTripwire);
            }
            foreach (var note in plan.Notes) EditorGUILayout.HelpBox(note, MessageType.Info);

            if (plan.Files.Count > 0)
            {
                EditorGUILayout.LabelField(T("Selected", "選んだもの"), string.Join(", ", selection.Select(Path.GetFileName)), EditorStyles.wordWrappedLabel);
                showFiles = EditorGUILayout.Foldout(showFiles, T("Goes in: " + plan.Files.Count + " files", "書き出すもの: " + plan.Files.Count + " 個"), true);
                if (showFiles)
                    using (new EditorGUI.IndentLevelScope())
                        foreach (var f in plan.Files)
                            EditorGUILayout.LabelField(f + (plan.Stripped.Contains(f) ? T("  (Tripwire taken out)", "  （Tripwire を外す）") : ""), EditorStyles.miniLabel);
                EditorGUILayout.LabelField(T("Tripwire taken out of", "Tripwire を外すもの"), plan.Stripped.Count == 0 ? T("none", "なし") : string.Join(", ", plan.Stripped.Select(Path.GetFileName)), EditorStyles.wordWrappedLabel);
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField(T("The person receiving it needs these packages (not included):", "受け取る人が入れておくパッケージ（書き出しには含めません）:"), EditorStyles.boldLabel);
                foreach (var p in plan.Packages) EditorGUILayout.LabelField("・" + p, EditorStyles.miniLabel);
                if (plan.Packages.Count == 0) EditorGUILayout.LabelField(T("none", "なし"), EditorStyles.miniLabel);
            }
            EditorGUILayout.EndScrollView();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent(T("Use the current selection", "いまの選択で作り直す"), T("Check again with what is selected in the Project window now.", "いま Project で選んでいるもので、もう一度確かめます。")))) Refresh();
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(!plan.CanWrite))
                    if (GUILayout.Button(T("Export…", "書き出す…"), GUILayout.Width(120))) Export();
            }
        }

        void Export()
        {
            var name = selection.Count == 1 ? Path.GetFileNameWithoutExtension(selection[0]) : "Export";
            var path = EditorUtility.SaveFilePanel(T("Export for Distribution", "配布用に書き出す"), "", name + ".unitypackage", "unitypackage");
            if (string.IsNullOrEmpty(path)) return;
            plan = TripwireExport.MakePlan(selection, keepTripwire); // what is shown, checked again: the files may have changed since
            if (!plan.CanWrite) return;
            TripwireExport.Write(plan, path);
            Debug.Log("[Tripwire] " + T("Exported " + plan.Files.Count + " files to " + path, path + " に " + plan.Files.Count + " 個のファイルを書き出しました。"));
            ShowNotification(new GUIContent(T("Exported.", "書き出しました。")));
            EditorUtility.RevealInFinder(path);
        }
    }
}
