using System.Collections.Generic;
using System.Linq;

namespace Tripwire.Core
{
    /// <summary>
    /// Ready-made settings for well-known world assets: which name each notification calls back, how to register for
    /// it, and their common operations. Only names and signatures, read from each asset's public source (no asset code).
    /// Versions checked: ProTV 3.0.0-beta.29.4, VizVid 1.8.2, VideoTXL 2.5.1, USharpVideo 1.0.1.
    /// </summary>
    public static class AssetPresets
    {
        public sealed class Preset
        {
            public string Id;
            public string NameEn, NameJa;
            /// <summary>Script class; subclasses match too (VideoTXL's SyncPlayer / LocalPlayer).</summary>
            public string ScriptType;
            public List<Notification> Notifications = new List<Notification>();
            public List<Operation> Operations = new List<Operation>();
        }

        public sealed class Notification
        {
            /// <summary>The name the asset calls on its listeners.</summary>
            public string Callback;
            public string En, Ja;
            public string RegisterMethod;
            /// <summary>Parameter count of the registration overload (ProTV's (listener, priority = 0) is 2).</summary>
            public int RegisterParamCount;
            /// <summary>Values for registration parameters other than the listener: index → int / string / "{callback}".</summary>
            public Dictionary<int, object> RegisterArgs = new Dictionary<int, object>();
            /// <summary>Parameterless method to call before registering (VideoTXL's _EnsureInit), or null.</summary>
            public string Prepare;
            /// <summary>Only offered for this subclass (the one that actually sends it), or null for the preset's type.</summary>
            public string OnlyFor;
        }

        public sealed class Operation
        {
            public string En, Ja;
            /// <summary>Calls in order; the user fills the last one's parameters.</summary>
            public List<Step> Steps = new List<Step>();
        }

        public sealed class Step
        {
            /// <summary>Method name, or "set:Name" for a property / field.</summary>
            public string Member;
            /// <summary>Parameter type names (C# full names) picking the overload; null for a property.</summary>
            public string[] ParamTypes = new string[0];
            /// <summary>Initial values for the parameters (index → value).</summary>
            public Dictionary<int, object> Defaults = new Dictionary<int, object>();
            /// <summary>Labels for the parameters (index → (en, ja)); a null label hides an internal parameter (its default is used).</summary>
            public Dictionary<int, (string En, string Ja)?> Labels = new Dictionary<int, (string En, string Ja)?>();
        }

        public const string CallbackPlaceholder = "{callback}";

        static readonly List<Preset> all = new List<Preset>();
        public static IReadOnlyList<Preset> All => all;

        public static Preset Get(string id) => all.FirstOrDefault(p => p.Id == id);

        const string Url = "VRC.SDKBase.VRCUrl", F = "System.Single", B = "System.Boolean", I = "System.Int32", U8 = "System.Byte";

        static Step Call(string member, params string[] types) => new Step { Member = member, ParamTypes = types };
        static Step Call(string member, string[] types, Dictionary<int, object> defaults) => new Step { Member = member, ParamTypes = types, Defaults = defaults };
        static Step Set(string member) => new Step { Member = "set:" + member, ParamTypes = null };
        static Operation Op(string en, string ja, params Step[] steps) => new Operation { En = en, Ja = ja, Steps = steps.ToList() };

        /// <summary>Labels the last step's parameters in order; null hides one.</summary>
        static Operation Labeled(this Operation op, params (string En, string Ja)?[] labels)
        {
            var last = op.Steps[op.Steps.Count - 1];
            for (int i = 0; i < labels.Length; i++) last.Labels[i] = labels[i];
            return op;
        }

        static readonly (string, string)? UrlLabel = ("URL", "URL"), Volume = ("Volume (0-1)", "音量（0〜1）"), OnOff = ("On", "オンにする"),
            Seconds = ("Seconds", "秒"), Progress = ("Position (0-1)", "位置（0〜1）"), PlayerType = ("Player type (1 = first)", "使うプレイヤー（1 = 1つ目）"),
            Paused = ("Pause", "一時停止する"), Hidden = null;

        static AssetPresets()
        {
            // ProTV (ArchiTech): TVManager._RegisterListener(UdonSharpBehaviour listener, sbyte priority = 0); events by SendCustomEvent.
            var protv = new Preset { Id = "protv", NameEn = "ProTV", NameJa = "ProTV", ScriptType = "ArchiTech.ProTV.TVManager" };
            foreach (var (cb, en, ja) in new[]
                     {
                         ("_TvPlay", "Started playing", "再生されたとき"),
                         ("_TvPause", "Paused", "一時停止されたとき"),
                         ("_TvStop", "Stopped", "停止されたとき"),
                         ("_TvMediaReady", "Media ready", "動画の準備ができたとき"),
                         ("_TvMediaEnd", "Media ended", "動画が最後まで再生されたとき"),
                         ("_TvMediaLoop", "Media looped", "動画がループしたとき"),
                         ("_TvMediaChange", "Media changed", "動画が変わったとき"),
                         ("_TvLoading", "Loading started", "読み込みが始まったとき"),
                         ("_TvVideoPlayerError", "Video error", "動画でエラーが起きたとき"),
                         ("_TvVolumeChange", "Volume changed", "音量が変わったとき"),
                         ("_TvMute", "Muted", "ミュートされたとき"),
                         ("_TvUnMute", "Unmuted", "ミュートが解除されたとき"),
                         ("_TvLock", "Locked", "ロックされたとき"),
                         ("_TvUnLock", "Unlocked", "ロックが解除されたとき"),
                         ("_TvOwnerChange", "Owner changed", "操作する人が変わったとき"),
                     })
                protv.Notifications.Add(new Notification { Callback = cb, En = en, Ja = ja, RegisterMethod = "_RegisterListener", RegisterParamCount = 2 });
            protv.Operations.Add(Op("Play URL", "URL の動画を再生", Call("_ChangeMedia", Url)).Labeled(UrlLabel));
            protv.Operations.Add(Op("Play", "再生", Call("_Play")));
            protv.Operations.Add(Op("Pause", "一時停止", Call("_Pause")));
            protv.Operations.Add(Op("Play / pause", "再生と一時停止を切り替え", Call("_TogglePlay")));
            protv.Operations.Add(Op("Stop", "停止", Call("_Stop")));
            protv.Operations.Add(Op("Skip", "次へ（スキップ）", Call("_Skip")));
            protv.Operations.Add(Op("Set volume (0-1)", "音量を変える（0〜1）", Call("_ChangeVolume", new[] { F, B }, new Dictionary<int, object> { { 0, 0.5f }, { 1, false } })).Labeled(Volume, Hidden));
            protv.Operations.Add(Op("Mute on / off", "ミュートを切り替え", Call("_ChangeMute", B)).Labeled(OnOff));
            protv.Operations.Add(Op("Loop on / off", "ループを切り替え", Call("_ChangeLoop", B)).Labeled(OnOff));
            protv.Operations.Add(Op("Seek (seconds)", "再生位置を変える（秒）", Call("_ChangeSeekTime", new[] { F, B }, new Dictionary<int, object> { { 1, false } })).Labeled(Seconds, Hidden));
            protv.Operations.Add(Op("Resync", "同期し直す", Call("_ReSync")));
            all.Add(protv);

            // VizVid (VVMW) Core: UdonSharpEventSender._AddListener(UdonSharpBehaviour callback).
            var vizvid = new Preset { Id = "vizvid", NameEn = "VizVid (player core)", NameJa = "VizVid（プレイヤー本体）", ScriptType = "JLChnToZ.VRC.VVMW.Core" };
            foreach (var (cb, en, ja) in new[]
                     {
                         ("_onVideoStart", "Video started", "動画が始まったとき"),
                         ("_onVideoEnd", "Video ended", "動画が終わったとき"),
                         ("_onVideoLoop", "Video looped", "動画がループしたとき"),
                         ("_onVideoReady", "Video ready", "動画の準備ができたとき"),
                         ("_OnVideoBeginLoad", "Loading started", "読み込みが始まったとき"),
                         ("_OnVideoError", "Video error", "動画でエラーが起きたとき"),
                         ("_OnVolumeChange", "Volume changed", "音量が変わったとき"),
                     })
                vizvid.Notifications.Add(new Notification { Callback = cb, En = en, Ja = ja, RegisterMethod = "_AddListener", RegisterParamCount = 1 });
            vizvid.Operations.Add(Op("Play URL", "URL の動画を再生", Call("PlayUrl", new[] { Url, U8 }, new Dictionary<int, object> { { 1, 1 } })).Labeled(UrlLabel, PlayerType));
            vizvid.Operations.Add(Op("Play", "再生", Call("Play")));
            vizvid.Operations.Add(Op("Pause", "一時停止", Call("Pause")));
            vizvid.Operations.Add(Op("Stop", "停止", Call("Stop")));
            vizvid.Operations.Add(Op("Set volume (0-1)", "音量を変える（0〜1）", Set("Volume")).Labeled(Volume));
            vizvid.Operations.Add(Op("Mute on / off", "ミュートを切り替え", Set("Muted")).Labeled(OnOff));
            vizvid.Operations.Add(Op("Loop on / off", "ループを切り替え", Set("Loop")).Labeled(OnOff));
            all.Add(vizvid);

            // VizVid FrontendHandler (playlist / queue layer, what VizVid's own UI uses).
            var vizvidUi = new Preset { Id = "vizvid-frontend", NameEn = "VizVid (playlist / controls)", NameJa = "VizVid（プレイリスト・操作）", ScriptType = "JLChnToZ.VRC.VVMW.FrontendHandler" };
            foreach (var (cb, en, ja) in new[]
                     {
                         // Sent from _Play() etc. only: on the screen of whoever pressed the control.
                         ("_OnPlay", "Play pressed (on the presser's screen only)", "再生が操作されたとき（操作した人の画面だけ）"),
                         ("_OnPause", "Pause pressed (on the presser's screen only)", "一時停止が操作されたとき（操作した人の画面だけ）"),
                         ("_OnStop", "Stop pressed (on the presser's screen only)", "停止が操作されたとき（操作した人の画面だけ）"),
                         ("_OnSkip", "Skip pressed (on the presser's screen only)", "スキップが操作されたとき（操作した人の画面だけ）"),
                     })
                vizvidUi.Notifications.Add(new Notification { Callback = cb, En = en, Ja = ja, RegisterMethod = "_AddListener", RegisterParamCount = 1 });
            vizvidUi.Operations.Add(Op("Play", "再生", Call("_Play")));
            vizvidUi.Operations.Add(Op("Pause", "一時停止", Call("_Pause")));
            vizvidUi.Operations.Add(Op("Stop", "停止", Call("_Stop")));
            vizvidUi.Operations.Add(Op("Skip", "次へ（スキップ）", Call("_Skip")));
            vizvidUi.Operations.Add(Op("Add URL to the queue", "URL を再生待ちに追加して再生", Call("PlayUrl", new[] { Url, U8 }, new Dictionary<int, object> { { 1, 1 } })).Labeled(UrlLabel, PlayerType));
            all.Add(vizvidUi);

            // USharpVideo (MerlinVR): RegisterCallbackReceiver(UdonSharpBehaviour); stop / pause / seek work only for the owner.
            var usv = new Preset { Id = "usharpvideo", NameEn = "USharpVideo", NameJa = "USharpVideo", ScriptType = "UdonSharp.Video.USharpVideoPlayer" };
            foreach (var (cb, en, ja) in new[]
                     {
                         ("OnUSharpVideoPlay", "Started playing", "再生されたとき"),
                         ("OnUSharpVideoPause", "Paused", "一時停止されたとき"),
                         ("OnUSharpVideoUnpause", "Resumed", "再開されたとき"),
                         ("OnUSharpVideoStop", "Stopped (on the owner's screen only)", "停止されたとき（オーナーの画面だけ）"),
                         ("OnUSharpVideoEnd", "Video ended", "動画が終わったとき"),
                         ("OnUSharpVideoLoadStart", "Loading started", "読み込みが始まったとき"),
                         ("OnUSharpVideoError", "Video error", "動画でエラーが起きたとき"),
                         ("OnUSharpVideoLockChange", "Lock changed", "ロックが切り替わったとき"),
                         ("OnUSharpVideoOwnershipChange", "Owner changed", "操作する人が変わったとき"),
                     })
                usv.Notifications.Add(new Notification { Callback = cb, En = en, Ja = ja, RegisterMethod = "RegisterCallbackReceiver", RegisterParamCount = 1 });
            usv.Operations.Add(Op("Play URL", "URL の動画を再生", Call("PlayVideo", Url)).Labeled(UrlLabel));
            usv.Operations.Add(Op("Stop (becomes owner first)", "停止（先にオーナーになる）", Call("TakeOwnership"), Call("StopVideo")));
            usv.Operations.Add(Op("Pause / resume (becomes owner first)", "一時停止・再開（先にオーナーになる）", Call("TakeOwnership"), Call("SetPaused", B)).Labeled(Paused));
            usv.Operations.Add(Op("Seek 0-1 (becomes owner first)", "再生位置を変える 0〜1（先にオーナーになる）", Call("TakeOwnership"), Call("SeekTo", F)).Labeled(Progress));
            usv.Operations.Add(Op("Loop on / off (becomes owner first)", "ループを切り替え（先にオーナーになる）", Call("TakeOwnership"), Call("SetLooping", B)).Labeled(OnOff));
            usv.Operations.Add(Op("Set volume (0-1)", "音量を変える（0〜1）", Call("SetVolume", new[] { F }, new Dictionary<int, object> { { 0, 0.5f } })).Labeled(Volume));
            usv.Operations.Add(Op("Mute on / off", "ミュートを切り替え", Call("SetMuted", B)).Labeled(OnOff));
            usv.Operations.Add(Op("Reload", "読み込み直す", Call("Reload")));
            all.Add(usv);

            // VideoTXL (Texelsaur): EventBase._Register(int eventIndex, Component handler, string eventName, params string[] args);
            // the listener picks the callback name. Registering before the player initialized fails, so _EnsureInit first.
            var txl = new Preset { Id = "videotxl", NameEn = "VideoTXL", NameJa = "VideoTXL", ScriptType = "Texel.TXLVideoPlayer" };
            // LocalPlayer only sends the state update; the others come from SyncPlayer.
            foreach (var (cb, index, en, ja, only) in new[]
                     {
                         ("_TxlStateUpdate", 0, "Playback state changed (playing, stopped, error...)", "再生状態が変わったとき（再生・停止・エラーなど）", (string)null),
                         ("_TxlVideoReady", 5, "Video ready", "動画の準備ができたとき", "Texel.SyncPlayer"),
                         ("_TxlInfoUpdate", 2, "Video info changed (URL...)", "動画の情報（URL など）が変わったとき", "Texel.SyncPlayer"),
                         ("_TxlLockUpdate", 3, "Lock changed", "ロックが切り替わったとき", "Texel.SyncPlayer"),
                     })
                txl.Notifications.Add(new Notification
                {
                    Callback = cb, En = en, Ja = ja, RegisterMethod = "_Register", RegisterParamCount = 4, Prepare = "_EnsureInit", OnlyFor = only,
                    RegisterArgs = new Dictionary<int, object> { { 0, index }, { 2, CallbackPlaceholder } },
                });
            txl.Operations.Add(Op("Play URL", "URL の動画を再生", Call("_ChangeUrl", Url)).Labeled(UrlLabel));
            txl.Operations.Add(Op("Play", "再生", Call("_TriggerPlay")));
            txl.Operations.Add(Op("Pause / resume", "一時停止・再開", Call("_TriggerPause")));
            txl.Operations.Add(Op("Stop", "停止", Call("_TriggerStop")));
            txl.Operations.Add(Op("Lock on / off", "ロックを切り替え", Call("_SetLock", B)).Labeled(OnOff));
            txl.Operations.Add(Op("Seek (seconds)", "再生位置を変える（秒）", Call("_SetTargetTime", F)).Labeled(Seconds));
            all.Add(txl);
        }
    }
}
