using System;
using System.Collections.Generic;
using System.Linq;
using Tripwire.Core;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tripwire.Editor
{
    // The trigger Inspector while it has no events: what to do first, and starter cards.
    internal sealed partial class TripwireTriggerEditor
    {
        // ---------------- empty state ----------------

        /// <summary>A common first gimmick, made with one click; the person then drags in the objects.</summary>
        internal sealed class Starter
        {
            public string En, Ja;
            /// <summary>An icon under Editor/Icons, drawn in <see cref="Color"/>'s category color.</summary>
            public string Icon, Color;
            public Action<TripwireTrigger> Make;
        }

        internal static readonly Starter[] Starters =
        {
            new Starter
            {
                En = "Click to show / hide", Icon = "Events/Interact", Color = "Common", Ja = "クリックで表示を切り替える",
                Make = t => t.events.Add(new KEvent { eventId = EventCatalog.InteractId, actions = { NewAction("GameObject.ToggleActive") } }),
            },
            new Starter
            {
                En = "Play a sound when a player walks in", Icon = "Actions/AudioSource.Play", Color = "SoundFx", Ja = "範囲に入ったら音を鳴らす",
                Make = t => t.events.Add(new KEvent { eventId = "OnPlayerTriggerEnter", actions = { NewAction("AudioSource.Play") } }),
            },
            new Starter
            {
                En = "A switch everyone shares", Icon = "Ui/Switch", Color = "Network", Ja = "全員で共有するスイッチ",
                Make = t =>
                {
                    // Clicking flips a synced on/off; whenever it changes (also for late joiners) the object follows it.
                    var name = UniqueName(Texts.T("switch", "スイッチ"), t.variables.Select(v => v.name));
                    t.variables.Add(new KVariable { name = name, typeName = "System.Boolean", synced = true });
                    var toggle = NewAction(ActionCatalog.ToggleVariableId);
                    toggle.args[0].stringValue = name;
                    t.events.Add(new KEvent { eventId = EventCatalog.InteractId, actions = { toggle } });
                    var show = NewAction("GameObject.SetActive");
                    show.args[1] = new KArg { source = KArgSource.Variable, name = name };
                    t.events.Add(new KEvent { eventId = EventCatalog.VariableChangedId, name = name, actions = { show } });
                },
            },
            new Starter
            {
                // Everyone hears it when anyone joins (the event's own default is the joining player's screen only).
                En = "A chime when someone joins", Icon = "Events/OnPlayerJoined", Color = "Player", Ja = "人が入ってきたらチャイム",
                Make = t => t.events.Add(new KEvent { eventId = "OnPlayerJoined", playerFilter = KPlayerFilter.Anyone, actions = { NewAction("AudioSource.Play") } }),
            },
            new Starter
            {
                En = "A UI button that shows / hides", Icon = "Categories/UI", Color = "UI", Ja = "UI のボタンで表示を切り替える",
                Make = t => t.events.Add(new KEvent { eventId = "UiButtonClick", actions = { NewAction("GameObject.ToggleActive") } }),
            },
            new Starter
            {
                En = "Blink every second", Icon = "Events/Timer", Color = "Common", Ja = "1 秒ごとに点滅する",
                Make = t => t.events.Add(new KEvent { eventId = EventCatalog.TimerId, name = Texts.T("blink", "点滅"), actions = { NewAction("GameObject.ToggleActive") } }),
            },
            new Starter
            {
                // A new, empty list of names; the person adds the admins' display names to it.
                En = "A button only admins can press", Icon = "Events/Interact", Color = "Network", Ja = "管理者だけが押せるボタン",
                Make = t => t.events.Add(new KEvent { eventId = EventCatalog.InteractId, gate = KGate.InList, gateList = NewPlayerList(), actions = { NewAction("GameObject.ToggleActive") } }),
            },
            new Starter
            {
                En = "Put objects back where they were", Icon = "Categories/Move", Color = "Move", Ja = "物を元の場所に戻す",
                Make = t => t.events.Add(new KEvent { eventId = EventCatalog.InteractId, actions = { NewAction(ActionCatalog.RespawnId) } }),
            },
            new Starter
            {
                En = "Show how many players are here", Icon = "Events/OnPlayerJoined", Color = "Player", Ja = "いる人数を表示する",
                Make = t =>
                {
                    KEvent Show(string id, float delay)
                    {
                        var text = NewAction("Text.SetText");
                        text.args[1].stringValue = Texts.T("{playerCount} players", "{プレイヤー数} 人");
                        return new KEvent { eventId = id, playerFilter = KPlayerFilter.Anyone, delaySeconds = delay, actions = { text } };
                    }
                    t.events.Add(Show("OnPlayerJoined", 0));
                    // A second later: whether the count still includes the player who is leaving isn't documented.
                    t.events.Add(Show("OnPlayerLeft", 1));
                },
            },
            new Starter
            {
                En = "Count down from 10", Icon = "Events/Timer", Color = "Common", Ja = "10 からカウントダウン",
                Make = t =>
                {
                    // Clicking sets the count and starts a timer; each second takes one off; the text follows; at 0 it stops.
                    var left = UniqueName(Texts.T("left", "残り"), t.variables.Select(v => v.name));
                    var timer = Texts.T("countdown", "カウントダウン");
                    t.variables.Add(new KVariable { name = left, typeName = "System.Int32" });
                    var set = NewAction(ActionCatalog.SetVariableId);
                    set.args[0].stringValue = left;
                    set.args[1].intValue = 10;
                    var start = NewAction(ActionCatalog.TimerStartId);
                    start.args[0].stringValue = timer;
                    t.events.Add(new KEvent { eventId = EventCatalog.InteractId, actions = { set, start } });
                    var minus = NewAction(ActionCatalog.AddVariableId);
                    minus.args[0].stringValue = left;
                    minus.args[1].intValue = -1;
                    t.events.Add(new KEvent { eventId = EventCatalog.TimerId, name = timer, timerAutoStart = false, actions = { minus } });
                    var text = NewAction("Text.SetText");
                    text.args[1].stringValue = "{" + left + "}";
                    t.events.Add(new KEvent { eventId = EventCatalog.VariableChangedId, name = left, actions = { text } });
                    var stop = NewAction(ActionCatalog.TimerStopId);
                    stop.args[0].stringValue = timer;
                    t.events.Add(new KEvent { eventId = EventCatalog.VariableChangedId, name = left, actions = { stop },
                                              conditions = { new KCondition { variable = left, op = KCompareOp.LessOrEqual, value = new KArg { intValue = 0 } } } });
                },
            },
            new Starter
            {
                En = "Count each player's visits (saved)", Icon = "Categories/Saved", Color = "Variable", Ja = "来た回数を数える（保存）",
                Make = t =>
                {
                    var visits = UniqueName(Texts.T("visits", "来た回数"), t.variables.Select(v => v.name));
                    t.variables.Add(new KVariable { name = visits, typeName = "System.Int32", saved = true, saveKey = visits });
                    var add = NewAction(ActionCatalog.AddVariableId);
                    add.args[0].stringValue = visits;
                    add.args[1].intValue = 1;
                    var text = NewAction("Text.SetText");
                    text.args[1].stringValue = Texts.T("Visit {" + visits + "}", "{" + visits + "} 回目");
                    // After the saved value has come back (the card runs after it is restored).
                    t.events.Add(new KEvent { eventId = "OnPlayerRestored", actions = { add, text } });
                },
            },
        };

        static GUIStyle starterStyle;

        /// <summary>Shown while the trigger has no events: what to do first, and starter cards.</summary>
        void DrawEmptyGuide()
        {
            EditorGUILayout.HelpBox(T("Add a \"when\" (an event) and then what to do. Or start from one of these and drag your objects into it:",
                                      "まず「いつ」（イベント）を追加して、その中に「何をする」を並べます。下のひな形から始めて、オブジェクトをドラッグで入れることもできます。"), MessageType.Info);
            // Two equal columns (one when narrow), so the buttons line up.
            int columns = EditorGUIUtility.currentViewWidth > 360 ? 2 : 1;
            if (starterStyle == null) starterStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(30, 6, 0, 0) };
            foreach (var row in Starters.Select((s, i) => (s, i)).GroupBy(x => x.i / columns))
            {
                var line = GUILayoutUtility.GetRect(0, 28, GUILayout.ExpandWidth(true));
                foreach (var (s, i) in row)
                {
                    float w = (line.width - 4 * (columns - 1)) / columns;
                    var r = new Rect(line.x + (i % columns) * (w + 4), line.y, w, line.height);
                    if (GUI.Button(r, new GUIContent(T(s.En, s.Ja)), starterStyle))
                    {
                        var starter = s;
                        Edit(() => starter.Make(t));
                    }
                    DrawTinted(new Rect(r.x + 9, r.y + 6, 16, 16), Icon(s.Icon), CategoryColor(s.Color));
                }
                GUILayout.Space(2);
            }
        }
    }
}
