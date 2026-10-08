using System.Collections.Generic;
using System.Linq;
using Tripwire.Core;
using UnityEditor;
using UnityEngine;

namespace Tripwire.Editor
{
    // The trigger Inspector: the header (name, version, mode, language, help links) and fix buttons for missing parts.
    internal sealed partial class TripwireTriggerEditor
    {
        static string version;
        const string DocsUrl = "https://haru0416-dev.github.io/tripwire/";
        const string IssuesUrl = "https://github.com/haru0416-dev/tripwire/issues";

        /// <summary>
        /// One line that fits a narrow Inspector: Tripwire (and its version when there is room), かんたん / くわしく, the
        /// trigger list, and a menu with the language and links.
        /// </summary>
        void DrawHeader()
        {
            if (version == null)
                version = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TripwireTriggerEditor).Assembly)?.version ?? "";
            // Everything on this line is RowHeight tall and centered in it.
            using (new EditorGUILayout.HorizontalScope(GUILayout.Height(RowHeight)))
            {
                HeaderTitle();
                if (version.Length > 0 && EditorGUIUtility.currentViewWidth > 380)
                    GUILayout.Label("v" + version, rowNote, GUILayout.ExpandWidth(false), GUILayout.Height(RowHeight));
                GUILayout.FlexibleSpace();
                int mode = GUILayout.Toolbar(TripwireSettings.Detailed ? 1 : 0, new[]
                {
                    new GUIContent(T("Simple", "かんたん"), T("Pickers show what most gimmicks use.", "選ぶ一覧に、よく使うものだけを出します。")),
                    new GUIContent(T("Detailed", "くわしく"), T("Pickers show every Udon event and action, with their Udon names.", "選ぶ一覧に、Udon のイベントやアクションをすべて、Udon での名前つきで出します。")),
                }, rowButton, GUILayout.Width(120), GUILayout.Height(RowHeight));
                if (mode == 1 != TripwireSettings.Detailed) { TripwireSettings.Detailed = mode == 1; GUIUtility.ExitGUI(); } // what follows changes
                GUILayout.Space(4);
                if (IconButton(Icon("Ui/List"), T("All triggers in the open scenes, with their state", "トリガー一覧（開いているシーンのトリガーを、状態つきで出します）"), rowButtonLeft))
                    TripwireOverviewWindow.Open();
                var menu = GUILayoutUtility.GetRect(26, RowHeight, rowButtonRight, GUILayout.Width(26), GUILayout.Height(RowHeight));
                if (IconButton(menu, Icon("Ui/Menu"), T("Language, help", "言語・使い方"), rowButtonRight)) HeaderMenu(menu);
            }
        }

        static void HeaderMenu(Rect at)
        {
            var m = new GenericMenu();
            m.AddItem(new GUIContent("言語 Language/日本語"), Texts.Japanese, () => TripwireSettings.SetLanguage(UiLanguage.Japanese));
            m.AddItem(new GUIContent("言語 Language/English"), !Texts.Japanese, () => TripwireSettings.SetLanguage(UiLanguage.English));
            m.AddSeparator("");
            m.AddItem(new GUIContent(T("How to use (manual)", "使い方（マニュアル）")), false, () => Application.OpenURL(DocsUrl));
            m.AddItem(new GUIContent(T("Report a problem", "不具合を報告")), false, () => Application.OpenURL(IssuesUrl));
            m.DropDown(at);
        }

        /// <summary>A row-high button with a 16 px icon in the text color.</summary>
        static bool IconButton(Texture icon, string tip, GUIStyle style) =>
            IconButton(GUILayoutUtility.GetRect(26, RowHeight, style, GUILayout.Width(26), GUILayout.Height(RowHeight)), icon, tip, style);

        static bool IconButton(Rect r, Texture icon, string tip, GUIStyle style)
        {
            bool clicked = GUI.Button(r, new GUIContent("", tip), style);
            DrawTinted(new Rect(r.x + (r.width - 16) / 2, r.y + (r.height - 16) / 2, 16, 16), icon, style.normal.textColor);
            return clicked;
        }

        // ---------------- fix buttons ----------------

        /// <summary>
        /// What an event needs on this object (a collider, a trigger zone, a pickup, a station, a video player, a UI
        /// shape): a note with a button that adds it, as one undo step.
        /// </summary>
        void DrawEventFixes(KEvent e, EventSpec spec)
        {
            foreach (var (message, button, apply) in FixesFor(t.gameObject, e, spec)) Fix(message, button, apply);
        }

        internal static IEnumerable<(string message, string button, System.Action apply)> FixesFor(GameObject go, KEvent e, EventSpec spec)
        {
            if (spec == null) yield break;
            var colliders = go.GetComponents<Collider>();
            if (EventCatalog.Needs(spec, EventNeed.Collider) && colliders.Length == 0)
                yield return (T("Clicking needs a collider on this object.", "クリックで使うには、このオブジェクトにコライダーが必要です。"),
                    T("Add a collider", "コライダーを付ける"), () => AddFittedCollider(go, false));
            // An area: a trigger collider of its own (an existing solid collider stays solid: a pickup must not fall through floors).
            if (EventCatalog.Needs(spec, EventNeed.Area) && !colliders.Any(c => c.isTrigger))
                yield return (colliders.Length == 0
                        ? T("This event needs an area: a collider with Is Trigger on.", "このイベントには範囲（Is Trigger をオンにしたコライダー）が必要です。")
                        : T("This object's colliders have Is Trigger off, so there is no area to enter.", "このオブジェクトのコライダーは Is Trigger がオフなので、入る範囲がありません。"),
                    T("Add an area", "範囲を付ける"), () => AddFittedCollider(go, true));
            if (EventCatalog.Needs(spec, EventNeed.Pickup) && go.GetComponent<VRC.SDK3.Components.VRCPickup>() == null)
                yield return (T("Pickup events need a VRC Pickup on this object.", "ピックアップのイベントには、このオブジェクトに VRC Pickup が必要です。"),
                    T("Add VRC Pickup", "VRC Pickup を付ける"), () =>
                    {
                        if (colliders.Length == 0) AddFittedCollider(go, false);
                        Undo.AddComponent<VRC.SDK3.Components.VRCPickup>(go); // brings a Rigidbody
                    });
            if (EventCatalog.Needs(spec, EventNeed.Station) && go.GetComponent<VRC.SDK3.Components.VRCStation>() == null)
                yield return (T("Station events need a VRC Station on this object.", "ステーションのイベントには、このオブジェクトに VRC Station が必要です。"),
                    T("Add VRC Station", "VRC Station を付ける"), () =>
                    {
                        if (colliders.Length == 0) AddFittedCollider(go, false); // players click it to sit
                        Undo.AddComponent<VRC.SDK3.Components.VRCStation>(go);
                    });
            if (spec.NeedsVideoPlayerOnSameObject && go.GetComponent<VRC.SDK3.Video.Components.Base.BaseVRCVideoPlayer>() == null)
                yield return (T("Video events only reach a trigger on the video player's own object.", "動画のイベントは、動画プレイヤーと同じオブジェクトのトリガーにだけ届きます。"),
                    T("Add a video player here", "ここに動画プレイヤーを付ける"), () => Undo.AddComponent<VRC.SDK3.Video.Components.VRCUnityVideoPlayer>(go));
            if (spec.Shape == EventShape.Ui && UiWiring.UiOf(e, spec) is Component ui && UiWiring.MissingUiShape(ui))
            {
                if (ui.GetComponentInParent<Canvas>(true) is Canvas canvas)
                    yield return (T("Players can't point at this UI without a VRC Ui Shape on its Canvas.", "Canvas に VRC Ui Shape がないので、VRChat でこの UI を押せません。"),
                        T("Add VRC Ui Shape", "VRC Ui Shape を付ける"), () => Undo.AddComponent<VRC.SDK3.Components.VRCUiShape>(canvas.rootCanvas.gameObject));
                else
                    yield return (T("This UI is not under a Canvas, so players can't point at it.", "この UI は Canvas の下にないので、VRChat で押せません。"), null, null);
            }
        }

        void Fix(string message, string button, System.Action apply)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.HelpBox(message, MessageType.Warning);
                if (button != null && GUILayout.Button(button, GUILayout.Width(Mathf.Max(96, GUI.skin.button.CalcSize(new GUIContent(button)).x + 12)), GUILayout.Height(38)))
                {
                    Undo.IncrementCurrentGroup();
                    apply();
                    Undo.SetCurrentGroupName(button);
                    Changed();
                    GUIUtility.ExitGUI();
                }
            }
        }

        /// <summary>A box collider around the object's renderers (or a unit box), as an area when isTrigger.</summary>
        static void AddFittedCollider(GameObject go, bool isTrigger)
        {
            var box = Undo.AddComponent<BoxCollider>(go);
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0 && !(go.GetComponent<Renderer>() is MeshRenderer))
            {
                var bounds = renderers[0].bounds;
                foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
                // The world box's 8 corners in local space (a rotated object would flatten a converted size).
                var local = new Bounds(go.transform.InverseTransformPoint(bounds.min), Vector3.zero);
                for (int k = 0; k < 8; k++)
                    local.Encapsulate(go.transform.InverseTransformPoint(new Vector3(
                        (k & 1) == 0 ? bounds.min.x : bounds.max.x, (k & 2) == 0 ? bounds.min.y : bounds.max.y, (k & 4) == 0 ? bounds.min.z : bounds.max.z)));
                box.center = local.center;
                box.size = local.size;
            }
            box.isTrigger = isTrigger;
        }
    }
}
