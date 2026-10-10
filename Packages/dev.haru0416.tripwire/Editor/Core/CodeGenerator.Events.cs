using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tripwire.Core
{
    // Events: resolving them, entry methods, dispatch, event bodies. Timers, listen events and conditions have files of their own.
    public static partial class CodeGenerator
    {
        sealed partial class Generator
        {
            HashSet<string> typeRoots;

            /// <summary>First segments of every type name this program writes in expressions (static calls, enum constants).</summary>
            HashSet<string> TypeRoots()
            {
                if (typeRoots != null) return typeRoots;
                typeRoots = new HashSet<string>(StringComparer.Ordinal);
                void Add(string typeName)
                {
                    if (string.IsNullOrEmpty(typeName)) return;
                    int dot = typeName.IndexOf('.');
                    typeRoots.Add(dot < 0 ? typeName : typeName.Substring(0, dot));
                }
                foreach (var e in p.Events)
                    if (e.Listen != null) Add(e.Listen.TargetType);
                foreach (var e in p.Events)
                    foreach (var a in ActionCall.Flatten(e.Actions))
                    {
                        if (a.Call != null)
                        {
                            Add(a.Call.DeclaringType);
                            foreach (var prm in a.Call.Params) Add(prm.Type.UnityType);
                            if (a.Call.Instance != null) Add(a.Call.Instance.UnityType);
                        }
                        var spec = ActionCatalog.Get(a.ActionId);
                        if (spec != null)
                            foreach (var prm in spec.Params)
                                if (prm.Type != null) Add(prm.Type.UnityType);
                    }
                return typeRoots;
            }

            EventSpec ResolveEvent(int i)
            {
                var e = p.Events[i];
                var spec = EventCatalog.Get(e.EventId);
                if (spec == null) { Error(Texts.T("This version of Tripwire has no event '" + e.EventId + "' (made with a newer version?). Update Tripwire, or remove the card.", "この版の Tripwire には「" + e.EventId + "」というイベントがありません（新しい版で作られたのかもしれません）。Tripwire を更新するか、このカードを外してください。"), ev: i); return null; }
                if (spec.Shape == EventShape.Custom)
                {
                    var problem = CheckCustomEventName(e.Name, TypeRoots());
                    if (problem != null) { Error(problem, ev: i); return null; }
                }
                if (spec.Shape == EventShape.Listen)
                {
                    var problem = CheckCallbackName(e.Name, TypeRoots());
                    if (problem != null) { Error(problem, ev: i); return null; }
                    if (p.Events.Any(o => o.EventId == EventCatalog.CustomId && o.Name == e.Name))
                    { Error(Texts.T("A Custom event already has this name. Rename one of them.", "同じ名前のカスタムイベントがあります。どちらかの名前を変えてください。"), ev: i); return null; }
                    if (e.Listen != null && e.Listen.RegisterMethod != null && !e.Listen.HasTarget)
                        Error(Texts.T("Pick the script to register with.", "「通知元」に、通知を送ってくるスクリプトが付いたオブジェクトを入れてください。"), ev: i);
                    // One method per name receives every call, whoever sends it: blocks sharing a name must share the sender.
                    string Key(EventBlock b) => b.Listen?.RegisterMethod == null ? "" : b.Listen.RegistrationKey ?? "";
                    if (p.Events.Take(i).Any(o => o.EventId == e.EventId && o.Name == e.Name && Key(o) != Key(e)))
                    { Error(Texts.T("Another block uses this callback name with a different sender or registration; the trigger couldn't tell them apart. Use a different name.",
                                    "同じ「届く名前」を、別の通知元（または別の登録方法）で使っています。どこから届いたか区別できないので、名前を分けてください。"), ev: i); return null; }
                    if (e.Broadcast != Broadcast.Local)
                        Warn(Texts.T("Scripts usually notify on every player's screen already; with broadcast " + e.Broadcast + " the actions may run once per player.",
                                     "通知はふつう各プレイヤーの画面でそれぞれ届きます。「自分だけ（Local）」以外にすると、人数分くり返し動くことがあります。"), ev: i);
                }
                if (spec.IsSyncEvent && !vars.Values.Any(v => v.Synced))
                    Warn(Texts.T("Without a synced variable this trigger never sends or receives sync, so this event never happens.",
                                 "同期する変数がないので、このトリガーは同期を送受信しません。このイベントは起きません。"), ev: i);
                if (e.Broadcast != Broadcast.Local && (spec.Frequent || spec.Id == EventCatalog.DeserializationId))
                    Warn(Texts.T("This event happens on every player's screen (often many times a second); with " + e.Broadcast + " each one also sends a network event, which can delay other sync. Set it to Only my screen (Local).",
                                 "このイベントは全員の画面でそれぞれ起きます（何度も起きるものもあります）。「自分だけ（Local）」以外にすると、そのたびにネットワークへ送られ、送る量が多すぎてほかの同期が遅れることがあります。「自分だけ（Local）」にしてください。"), ev: i);
                // Events that happen on every player's screen anyway: sent to everyone, each one runs once per player.
                if (e.Broadcast != Broadcast.Local && EveryPlayerEvents.Contains(spec.Id))
                    Warn(Texts.T("This event happens for every player on their own screen; with " + e.Broadcast + " the actions run once per player each time (every player's Start runs them again for everyone, for one). Set it to Only my screen (Local).",
                                 "このイベントは、各プレイヤーの画面でそれぞれ起きます。「自分だけ（Local）」以外にすると、そのたびに人数分くり返し動きます（たとえば Start なら、誰かが入るたびに全員の画面でもう一度動きます）。「自分だけ（Local）」にしてください。"), ev: i);
                if (spec.Shape == EventShape.Timer)
                {
                    var tm = e.Timer;
                    if (tm == null || tm.MinSeconds <= 0f || float.IsNaN(tm.MinSeconds) || float.IsInfinity(tm.MaxSeconds))
                    { Error(Texts.T("A timer needs more than 0 seconds.", "タイマーの秒数は 0 より大きくしてください。"), ev: i); return null; }
                    if (tm.MaxSeconds < tm.MinSeconds)
                    { Error(Texts.T("The maximum must be at least the minimum.", "最大の秒数が最小より短くなっています。直してください。"), ev: i); return null; }
                    if (!string.IsNullOrEmpty(e.Name) && p.Events.Take(i).Any(o => o.EventId == EventCatalog.TimerId && o.Name == e.Name))
                    { Error(Texts.T("Another timer has this name. Give this one another name.", "ほかのタイマーと名前が同じです。別の名前にしてください。"), ev: i); return null; }
                    if (e.Broadcast != Broadcast.Local)
                        Warn(Texts.T("Every player runs their own timer; with " + e.Broadcast + " the actions run once per player each time.",
                                     "タイマーは各プレイヤーの画面でそれぞれ動きます。「自分だけ（Local）」以外にすると、毎回人数分くり返し動きます。"), ev: i);
                }
                if (spec.Shape == EventShape.Ui && !e.HasUiSource)
                    // Like an action with no objects yet: a warning, so a fresh card isn't an error before anything is dragged in.
                    Warn(Texts.T("Nothing runs this card until its UI element (a button, toggle or slider) is put in.", "対象の UI（ボタン・トグル・スライダー）を入れるまで、このカードは動きません。"), ev: i);
                if (spec.Shape == EventShape.VariableChanged && vars.TryGetValue(e.Name ?? "", out var watchedVar) && watchedVar.Temporary)
                { Error(Texts.T("A temporary variable can't be watched (it only exists while an event runs).", "一時的な変数は、イベントが動いている間しかないので見張れません。"), ev: i); return null; }
                if (spec.Shape == EventShape.VariableChanged)
                {
                    if (e.Name == null || !vars.ContainsKey(e.Name)) { Error(Texts.T("On Variable Changed needs an existing variable.", "見張る変数を選んでください。"), ev: i); return null; }
                    if (e.Broadcast != Broadcast.Local)
                        Warn(Texts.T("Variable changes already reach every player; with broadcast " + e.Broadcast + " the actions run several times.", "変数の変化はもともと全員に届きます。「自分だけ（Local）」以外にすると、同じ処理が何度も動きます。"), ev: i);
                }
                if (e.DelaySeconds < 0f || float.IsNaN(e.DelaySeconds) || float.IsInfinity(e.DelaySeconds))
                    Error(Texts.T("Delay must be zero or more seconds.", "遅らせる秒数は 0 以上にしてください。"), ev: i);
                if (spec.Frequent) WarnFrequent(e, i);
                if (e.Gate != Gate.Anyone && e.EventId == EventCatalog.VariableChangedId && vars.TryGetValue(e.Name ?? "", out var gatedVar) && gatedVar.Synced)
                    Warn(Texts.T("A synced variable's change runs on every screen, and each checks its own player: the result differs between screens. Limit the card that changes it instead.",
                                 "同期する変数の「変わったとき」は全員の画面で動き、それぞれの画面の人で確かめるので、画面ごとに結果が変わります。変数を変える側のカードで絞ってください。"), ev: i);
                if (e.Gate != Gate.Anyone && e.EventId == EventCatalog.CustomId && SentHereOverNetwork(e.Name))
                    Warn(Texts.T("This event is sent here over the network: each screen checks its own player, not the sender. Limit the card that sends it instead.",
                                 "このイベントはネットワークで送られてくるので、送った人ではなく、受け取った画面の人で確かめます。送る側のカードで絞ってください。"), ev: i);
                if (e.Gate == Gate.InList || e.Gate == Gate.NotInList)
                {
                    if (e.GateNames == null) { Error(Texts.T("Pick the list of players.", "プレイヤーのリストを選んでください。"), ev: i); return null; }
                    if (e.GateNames.Count == 0)
                        Warn(e.Gate == Gate.InList ? Texts.T("The list has no names, so nobody can use this.", "リストに名前がないので、誰も使えません。")
                                                   : Texts.T("The list has no names, so everyone can use this.", "リストに名前がないので、誰でも使えます。"), ev: i);
                }
                return spec;
            }

            // Methods reachable over the network are exactly the ones whose name does not start with '_' and that are public.
            // Local-only bodies get a '_' name so a modified client cannot run them on everyone via SendCustomNetworkEvent.
            string BodyName(int i) => (p.Events[i].Broadcast == Broadcast.Local ? "_Tw_E" : "Tw_E") + i;

            string Dispatch(int i)
            {
                string run;
                switch (p.Events[i].Broadcast)
                {
                    case Broadcast.All: run = "SendCustomNetworkEvent(NetworkEventTarget.All, \"" + BodyName(i) + "\");"; break;
                    case Broadcast.Owner: run = "SendCustomNetworkEvent(NetworkEventTarget.Owner, \"" + BodyName(i) + "\");"; break;
                    default: run = BodyName(i) + "();"; break;
                }
                // Who may set it off: checked here, where it fires, before anything is sent.
                var gate = GateTest(i);
                return gate == null ? run : "if (" + gate + ") " + run;
            }

            /// <summary>The test of a block's "Who can use it", or null for anyone.</summary>
            string GateTest(int i)
            {
                switch (p.Events[i].Gate)
                {
                    case Gate.Owner: return "Networking.IsOwner(gameObject)";
                    case Gate.Master: return "Networking.IsMaster";
                    case Gate.InstanceOwner: return "Networking.IsInstanceOwner";
                    case Gate.InList: return "(tw_Known || Tw_Know()) && " + InList(i);
                    case Gate.NotInList: return "!((tw_Known || Tw_Know()) && " + InList(i) + ")";
                    default: return null;
                }
            }

            /// <summary>The lists of names the gates use, one per distinct list (cards sharing a list share its answer).</summary>
            readonly List<List<string>> gateLists = new List<List<string>>();

            /// <summary>The field saying whether the local player is in a block's list (filled once by Tw_Know).</summary>
            string InList(int i)
            {
                var names = p.Events[i].GateNames ?? new List<string>();
                int n = gateLists.FindIndex(l => l.SequenceEqual(names));
                if (n < 0) { gateLists.Add(names); n = gateLists.Count - 1; }
                return "tw_In" + n;
            }

            /// <summary>Whether this trigger sends itself the Custom event <paramref name="name"/> to all players or the owner.</summary>
            bool SentHereOverNetwork(string name) => p.Events.Any(e => ActionCall.Flatten(e.Actions).Any(a =>
                (a.ActionId == ActionCatalog.SendEventId || a.ActionId == ActionCatalog.SendRandomEventId)
                && a.Args.Count > 2 && a.Args[0]?.Source == ArgSource.Self && a.Args[2]?.Constant is int how && how != 0
                && a.Args[1]?.Constant is string names && (a.ActionId == ActionCatalog.SendEventId ? names == name : RandomEventNames(names).Contains(name))));

            /// <summary>
            /// Whether the local player is in each list, worked out the first time a gate asks: their name and the lists
            /// stay the same for the whole visit, so a gate on a frequent event costs a field read.
            /// </summary>
            void EmitGates()
            {
                if (gateLists.Count == 0) return;
                // The names as data, not code: filled by the editor (ConstantsInFields) or as field initializers, which U#
                // also bakes into the program's data instead of building the array in code.
                fields.Append("        bool tw_Known;\n");
                for (int n = 0; n < gateLists.Count; n++)
                {
                    if (ConstantsInFields)
                    {
                        fields.Append("        public string[] tw_Gate").Append(n).Append(";\n");
                        Result.Bindings.Add(new FieldBinding { Field = "tw_Gate" + n, Kind = BindingKind.Constant, Constant = gateLists[n].ToArray() });
                    }
                    else fields.Append("        string[] tw_Gate").Append(n).Append(" = new string[] { ").Append(string.Join(", ", gateLists[n].Select(StringLiteral))).Append(" };\n");
                    fields.Append("        bool tw_In").Append(n).Append(";\n");
                }
                methods.Append("        bool Tw_Know()\n        {\n");
                methods.Append("            VRCPlayerApi tw_P = Networking.LocalPlayer;\n");
                methods.Append("            if (!Utilities.IsValid(tw_P)) return false;\n");
                methods.Append("            string tw_Name = tw_P.displayName;\n");
                for (int n = 0; n < gateLists.Count; n++)
                    methods.Append("            tw_In").Append(n).Append(" = Tw_Listed(tw_Gate").Append(n).Append(", tw_Name);\n");
                methods.Append("            tw_Known = true;\n            return true;\n        }\n\n");
                methods.Append("        bool Tw_Listed(string[] names, string name)\n        {\n");
                methods.Append("            for (int tw_I = 0; tw_I < names.Length; tw_I++) if (names[tw_I] == name) return true;\n");
                methods.Append("            return false;\n        }\n\n");
            }

            /// <summary>
            /// When every click card is for some players only, and that doesn't change during a visit (a list, the
            /// instance's creator), the others can't even point at it: DisableInteractive at Start.
            /// </summary>
            string InteractGate()
            {
                var clicks = Enumerable.Range(0, p.Events.Count).Where(i => p.Events[i].EventId == "Interact").ToList();
                if (clicks.Count == 0 || clicks.Any(i => p.Events[i].Gate != Gate.InstanceOwner && p.Events[i].Gate != Gate.InList && p.Events[i].Gate != Gate.NotInList)) return "";
                return "            DisableInteractive = !(" + string.Join(" || ", clicks.Select(GateTest).Distinct()) + ");\n";
            }

            /// <summary>Action index used for a listen event's registration arguments (diagnostics, URL field names).</summary>
            const int ListenArgsAction = -3;
            static readonly HashSet<string> EveryPlayerEvents = new HashSet<string> { "Start", "OnEnable", "OnDisable", "OnPlayerJoined", "OnPlayerLeft" };

            static string ArgField(EventSpec spec, string param) => "tw_Arg_" + spec.Method + "_" + param;

            /// <summary>
            /// Work that piles up or floods the network on an event that happens many times a second (every frame, every
            /// physics step, while staying in an area): each kind is reported once per event, on the first action doing it.
            /// </summary>
            void WarnFrequent(EventBlock e, int i)
            {
                if (e.DelaySeconds > 0f)
                    Warn(Texts.T("This event happens many times a second; delaying it schedules a new run each time, and they pile up. Use no delay, or a Timer.",
                                 "このイベントは 1\u00A0秒に何度も起きます。遅らせると、そのたびに予約が増えて積み重なります。遅らせずに使うか、タイマーを使ってください。"), ev: i);
                var flat = ActionCall.Flatten(e.Actions);
                var said = new HashSet<string>();
                void Once(string key, int act, string en, string ja) { if (said.Add(key)) Warn(Texts.T(en, ja), i, act); }
                for (int k = 0; k < flat.Count; k++)
                {
                    var a = flat[k];
                    if (a.ActionId == ActionCatalog.TimerStartId)
                        Once("timer", k, "Starting a timer many times a second restarts it each time, so it never fires.",
                             "1\u00A0秒に何度もタイマーを始めると、そのたびに始め直すので、タイマーが鳴りません。");
                    if (a.ActionId == ActionCatalog.SendEventDelayedId)
                        Once("delayed", k, "Sending a delayed event many times a second schedules a new one each time, and they pile up.",
                             "1\u00A0秒に何度も「何秒か後にカスタムイベントを呼ぶ」を動かすと、そのたびに予約が増えて積み重なります。");
                    if (a.ActionId == "Networking.TakeOwnership")
                        Once("owner", k, "Taking ownership many times a second makes players who run this at the same time take it from each other again and again.",
                             "1\u00A0秒に何度もオーナーになろうとすると、同時に動いている人どうしでオーナーを取り合い続けます。");
                    if (a.ActionId == ActionCatalog.GetComponentId)
                        Once("getcomponent", k, "Getting a component many times a second is slow in Udon. Get it once (in Start) into a variable and use that.",
                             "1\u00A0秒に何度もコンポーネントを取り出すと、Udon では重くなります。「開始したとき」に一度取り出して変数に入れ、それを使ってください。");
                    if (ChangedVariables(a).Any(changed => vars.TryGetValue(changed, out var cv) && cv.Synced))
                        Once("sync", k, "Changing a synced variable many times a second sends it each time it changes, which can delay other sync. Change it on a less frequent event.",
                             "1\u00A0秒に何度も同期する変数を変えると、変わるたびに送信され、ほかの同期が遅れることがあります。もっと少ない回数のイベントで変えてください。");
                    // PlayerData sends all of the player's data again on any change.
                    if (ChangedVariables(a).Any(changed => vars.TryGetValue(changed, out var sv) && sv.SaveKey != null))
                        Once("save", k, "Changing a saved variable many times a second sends all of the player's saved data each time it changes. Change it on a less frequent event.",
                             "1\u00A0秒に何度も保存する変数を変えると、変わるたびに、その人の保存データがすべて送られます。もっと少ない回数のイベントで変えてください。");
                }
            }

            /// <summary>The trigger variables an action writes.</summary>
            static IEnumerable<string> ChangedVariables(ActionCall a)
            {
                if (a.ActionId == ActionCatalog.GetRemoteId)
                    return a.Args.Count > 2 && a.Args[2]?.Constant is string into ? new[] { into } : new string[0];
                if (ActionCatalog.Get(a.ActionId)?.WritesVariable == true)
                    return a.Args.Count > 0 && a.Args[0]?.Constant is string name ? new[] { name } : new string[0];
                return a.CallOutputs();
            }

            void EmitEntries(EventSpec[] specs)
            {
                // Override / Unity message entries: one method per event type, running each block in order.
                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < specs.Length; i++)
                {
                    var spec = specs[i];
                    if (spec == null) continue;
                    if (spec.Shape == EventShape.Override || spec.Shape == EventShape.UnityMessage)
                    {
                        if (!seen.Add(spec.Id)) continue;
                        foreach (var ep in spec.Params)
                        {
                            var field = ArgField(spec, ep.Name);
                            if (declaredArgFields.Add(field))
                            {
                                fields.Append("        ").Append(TypeName(ep.Type)).Append(' ').Append(field).Append(";\n");
                                eventValueFields.Add((field, ep.Name));
                                fieldTypes[field] = TypeName(ep.Type);
                            }
                        }

                        var sig = string.Join(", ", spec.Params.Select(ep => TypeName(ep.Type) + " " + ep.Name));
                        methods.Append(spec.Shape == EventShape.Override ? "        public override void " : "        void ")
                            .Append(spec.Method).Append('(').Append(sig).Append(")\n        {\n");
                        if (spec.Method == EventCatalog.StartId) methods.Append(StartupCalls(specs));
                        if (spec.Method == "OnPlayerRestored" && SavesVariables()) methods.Append("            Tw_Restore(").Append(spec.Params[0].Name).Append(");\n");
                        if (spec.Method == EventCatalog.DeserializationId) methods.Append(UseSyncReceived());
                        foreach (var ep in spec.Params)
                            methods.Append("            ").Append(ArgField(spec, ep.Name)).Append(" = ").Append(ep.Name).Append(";\n");
                        for (int j = i; j < specs.Length; j++)
                        {
                            if (specs[j] == null || specs[j].Id != spec.Id) continue;
                            var filter = PlayerFilterExpr(spec, p.Events[j].PlayerFilter);
                            methods.Append("            ");
                            if (filter != null) methods.Append("if (").Append(filter).Append(") ");
                            methods.Append(Dispatch(j)).Append('\n');
                            if (spec.HasInteractText && Result.InteractText == null && !string.IsNullOrEmpty(p.Events[j].InteractText))
                                Result.InteractText = p.Events[j].InteractText;
                        }
                        // Notifications named like this event's internal name (VizVid's "_onVideoStart") arrive here too.
                        for (int j = 0; j < specs.Length; j++)
                            if (specs[j] != null && specs[j].Shape == EventShape.Listen && BuiltInEventFor(p.Events[j].Name)?.Method == spec.Method)
                                methods.Append("            ").Append(Dispatch(j)).Append('\n');
                        methods.Append("        }\n\n");
                    }
                }

                EmitListenEntries(specs, seen);
                EmitTimers(specs);
                // A Start method for what runs at start (registrations, timers) when the trigger has no Start event.
                if (!seen.Contains(EventCatalog.StartId) && StartupCalls(specs).Length > 0)
                    methods.Append("        void Start()\n        {\n").Append(StartupCalls(specs)).Append("        }\n\n");

                // Saved variables come back when the player's data is loaded, also when the trigger has no such event.
                if (!seen.Contains("OnPlayerRestored") && SavesVariables())
                    methods.Append("        public override void OnPlayerRestored(VRCPlayerApi player)\n        {\n            Tw_Restore(player);\n        }\n\n");

                // Custom events: one public method per name.
                var customNames = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < specs.Length; i++)
                {
                    if (specs[i] == null || specs[i].Shape != EventShape.Custom) continue;
                    var name = p.Events[i].Name;
                    if (!customNames.Add(name)) continue;
                    methods.Append("        public void ").Append(name).Append("()\n        {\n");
                    for (int j = i; j < specs.Length; j++)
                        if (specs[j] != null && specs[j].Shape == EventShape.Custom && p.Events[j].Name == name)
                            methods.Append("            ").Append(Dispatch(j)).Append('\n');
                    methods.Append("        }\n\n");
                }

                // UI events: one public method per block, which the editor registers on the element's UnityEvent
                // (UI can only call UdonBehaviour.SendCustomEvent, and not underscore methods for sure, so it is public).
                for (int i = 0; i < specs.Length; i++)
                {
                    var spec = specs[i];
                    if (spec == null || spec.Shape != EventShape.Ui) continue;
                    var ui = "tw_Ui" + i;
                    fields.Append("        public ").Append(spec.UiType).Append(' ').Append(ui).Append(";\n");
                    Result.Bindings.Add(new FieldBinding { Field = ui, Kind = BindingKind.UiElement, Event = i, UnityType = spec.UiType, IsArray = false });
                    foreach (var ep in spec.Params)
                        fields.Append("        ").Append(TypeName(ep.Type)).Append(' ').Append(ArgFieldFor(i, spec, ep.Name)).Append(";\n");
                    methods.Append("        public void ").Append(UiMethod(i)).Append("()\n        {\n");
                    // Only the UI may run this: a network call (a modified client) is ignored.
                    methods.Append("            if (VRC.SDK3.UdonNetworkCalling.NetworkCalling.InNetworkCall) return;\n");
                    if (spec.UiValueMember != null)
                    {
                        methods.Append("            if (!Utilities.IsValid(").Append(ui).Append(")) return;\n");
                        methods.Append("            ").Append(ArgFieldFor(i, spec, spec.Params[0].Name)).Append(" = ").Append(ui).Append('.').Append(spec.UiValueMember).Append(";\n");
                    }
                    methods.Append("            ").Append(Dispatch(i)).Append("\n        }\n\n");
                }
            }

            /// <summary>The field holding an event's parameter. UI events are per block (each reads its own element).</summary>
            static string ArgFieldFor(int ev, EventSpec spec, string param) =>
                spec.Shape == EventShape.Ui ? "tw_Arg_Ui" + ev + "_" + param : ArgField(spec, param);

            /// <summary>Statements run first thing in Start: listener registration, auto-starting timers.</summary>
            string StartupCalls(EventSpec[] specs) =>
                InteractGate() + (RemembersHomes() ? "            Tw_RememberHomes();\n" : "")
                + (NeedsRegistration() ? "            Tw_Register();\n" : "") + (TimersToStart(specs).Any() ? "            Tw_StartTimers();\n" : "");

            /// <summary>Whether an OnDeserialization method came from event blocks (synced variables then join it).</summary>
            bool deserializationEmitted;

            /// <summary>Called when event blocks write OnDeserialization: the variables' change checks then go into Tw_SyncReceived.</summary>
            string UseSyncReceived()
            {
                deserializationEmitted = true;
                return vars.Values.Any(TracksReceivedChange) ? "            Tw_SyncReceived();\n" : "";
            }

            string PlayerFilterExpr(EventSpec spec, PlayerFilter filter)
            {
                if (spec.PlayerParam == null) return null;
                switch (filter)
                {
                    case PlayerFilter.LocalPlayer: return "Utilities.IsValid(" + spec.PlayerParam + ") && " + spec.PlayerParam + ".isLocal";
                    case PlayerFilter.OtherPlayers: return "Utilities.IsValid(" + spec.PlayerParam + ") && !" + spec.PlayerParam + ".isLocal";
                    default: return null;
                }
            }

            void EmitEventBody(int i, EventSpec spec)
            {
                var e = p.Events[i];
                var body = new StringBuilder();
                var fail = ConditionTest(e.Conditions, e.MatchAny, i, -1, holds: false);
                bool traced = Traced(spec);
                if (fail != null) body.Append("            if (").Append(fail).Append(traced ? ") { " + TraceStopped(i) + " return; }\n" : ") return;\n");
                if (traced) body.Append("            ").Append(TraceRan(i, spec)).Append('\n');
                EmitActions(i, spec, e.Actions, 0, body);
                // Temporary variables this event uses are its own locals, starting from the initial value (an event it
                // calls has its own copy, so neither sees the other's).
                var code = body.ToString();
                foreach (var v in vars.Values.Where(x => x.Temporary))
                    if (System.Text.RegularExpressions.Regex.IsMatch(code, @"\b" + FieldOf(v.Name) + @"\b"))
                        body.Insert(0, "            " + TypeName(v.Type) + " " + FieldOf(v.Name) + " = " + ConstantExpr(v.Kind, v.Initial ?? DefaultOf(v.Kind), "V" + p.Variables.IndexOf(v)) + ";\n");

                // Network-called bodies must be public; local ones stay private. The delayed half must be public (it is run by
                // name) but keeps a '_' name so it is not network-callable.
                var head = e.Broadcast == Broadcast.Local ? "        void " : "        public void ";
                if (e.DelaySeconds > 0f)
                {
                    methods.Append(head).Append(BodyName(i)).Append("()\n        {\n");
                    methods.Append("            SendCustomEventDelayedSeconds(\"_Tw_R").Append(i).Append("\", ").Append(ConstantExpr(ValueKind.Float, e.DelaySeconds, "Delay" + i)).Append(");\n");
                    methods.Append("        }\n\n");
                    methods.Append("        public void _Tw_R").Append(i).Append("()\n        {\n").Append(body).Append("        }\n\n");
                }
                else
                {
                    methods.Append(head).Append(BodyName(i)).Append("()\n        {\n").Append(body).Append("        }\n\n");
                }
            }
        }
    }
}
