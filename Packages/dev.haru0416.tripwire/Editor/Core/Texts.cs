using System;
using System.Collections.Generic;

namespace Tripwire.Core
{
    public enum UiLanguage { Japanese, English }

    /// <summary>
    /// Everything a user reads: catalog names, descriptions, categories, parameter labels, type names, diagnostics.
    /// English lives next to each entry in the catalogs; Japanese is here, keyed by id.
    /// </summary>
    public static class Texts
    {
        public static UiLanguage Language = UiLanguage.Japanese;

        public static bool Japanese => Language == UiLanguage.Japanese;

        public static string T(string en, string ja) => Japanese ? ja : en;

        // ---- categories (display order is the order of these lists) ----

        public static readonly string[] EventCategories = { "Common", "Pickup", "Player", "Physics", "Avatar", "Input", "UI", "Video", "Network", "Frame", "Load", "Economy", "Link", "Advanced" };
        public static readonly string[] ActionCategories = { "Show", "Move", "SoundFx", "Text", "Player", "Pickup", "Flow", "Variable", "Video", "Link", "Advanced" };
        /// <summary>Event categories the pickers hide in "かんたん" (Simple): what few gimmicks need.</summary>
        public static readonly string[] AdvancedCategories = { "Physics", "Avatar", "Input", "Network", "Frame", "Load", "Economy", "Advanced" };

        static readonly Dictionary<string, (string en, string ja)> categories = new Dictionary<string, (string, string)>
        {
            { "Common", ("Common", "よく使う") },
            { "Pickup", ("Pickups", "ピックアップ") },
            { "Player", ("Players", "プレイヤー") },
            { "Advanced", ("Advanced", "上級") },
            { "Physics", ("Physics & collisions", "当たり判定・物理") },
            { "Avatar", ("Avatars, contacts & PhysBones", "アバター・コンタクト・PhysBone") },
            { "Input", ("Input (buttons, MIDI)", "入力（ボタン・MIDI）") },
            { "Network", ("Sync & saved data", "同期・保存データ") },
            { "Frame", ("Every frame & rendering", "毎フレーム・描画") },
            { "Load", ("Loading text & images", "文字・画像の読み込み") },
            { "Economy", ("Store & purchases", "ストア・購入") },
            { "UI", ("UI (buttons, toggles, sliders)", "UI（ボタン・トグル・スライダー）") },
            { "Video", ("Video player", "動画プレイヤー") },
            { "Flow", ("If, loops, timers", "If・ループ・タイマー") },
            { "Show", ("Show / hide", "表示・非表示") },
            { "Move", ("Move", "移動・アニメーション") },
            { "SoundFx", ("Sound & effects", "音・エフェクト") },
            { "Text", ("Text", "テキスト") },
            { "Variable", ("Variables", "変数") },
            { "Link", ("Other triggers & scripts", "連携（イベント・スクリプト）") },
            { "Saved", ("Saved actions", "保存したアクション") },
        };

        public static string Category(string id)
        {
            (string en, string ja) c;
            return categories.TryGetValue(id ?? "", out c) ? T(c.en, c.ja) : id;
        }

        // ---- events ----

        static readonly Dictionary<string, (string name, string desc)> eventsJa = new Dictionary<string, (string, string)>
        {
            { "Interact", ("クリックしたとき", "このオブジェクトをクリック（使う）したとき。コライダーが必要です。") },
            { "OnPlayerTriggerEnter", ("プレイヤーが範囲に入ったとき", "プレイヤーがこのオブジェクトのコライダー（Is Trigger）の範囲に入ったとき。") },
            { "OnPlayerTriggerExit", ("プレイヤーが範囲から出たとき", "プレイヤーがこのオブジェクトのコライダー（Is Trigger）の範囲から出たとき。") },
            { "Start", ("開始したとき", "このオブジェクトが動き出したとき。ふつうはワールドを読み込んだときで、非表示で始まるオブジェクトなら最初に表示されたとき。各プレイヤーの画面で一度ずつ。") },
            { "Timer", ("タイマー（一定時間ごと）", "決めた秒数ごとに（または一度だけ）実行します。秒数を最小〜最大で決めると、毎回ランダムな間隔になります。オブジェクトを非表示にしても動き続けます（止めるときは「オブジェクトが無効になったとき」に「タイマーを止める」を置きます）。") },
            { "Update", ("毎フレーム", "毎フレーム実行します（重い処理は入れないでください）。") },
            { "Custom", ("カスタムイベント", "名前を付けたイベントです。ほかのトリガーの「カスタムイベントを呼ぶ」や、ほかのスクリプトから呼び出せます。") },
            { "OnVariableChanged", ("変数が変わったとき", "変数の値が変わったとき。同期した変数なら、ほかの人が変えたときや、あとから来たときも。") },
            { "OnPickup", ("ピックアップを持ったとき", "自分がこのピックアップを手に持ったとき。") },
            { "OnDrop", ("ピックアップを離したとき", "自分がこのピックアップを手から離したとき。") },
            { "OnPickupUseDown", ("ピックアップを使ったとき", "持った状態で Use（トリガー）ボタンを押したとき。") },
            { "OnPickupUseUp", ("ピックアップを使い終えたとき", "持った状態で Use（トリガー）ボタンを離したとき。") },
            { "OnPlayerJoined", ("プレイヤーが参加したとき", "プレイヤーがインスタンスに参加したとき（自分が参加したときは、先にいた人の分も届きます）。") },
            { "OnPlayerLeft", ("プレイヤーが退出したとき", "プレイヤーがインスタンスから退出したとき。") },
            { "OnPlayerRespawn", ("リスポーンしたとき", "プレイヤーがメニューの Respawn を押して、リスポーンしたとき。") },
            { "OnStationEntered", ("ステーションに座ったとき", "プレイヤーがこのステーション（VRC Station、椅子など）に座ったとき。") },
            { "OnStationExited", ("ステーションから降りたとき", "プレイヤーがこのステーションから降りたとき。") },
            { "OnPlayerCollisionEnter", ("プレイヤーがぶつかったとき", "プレイヤーがこのオブジェクトにぶつかったとき。") },
            { "OnEnable", ("オブジェクトが有効になったとき", "このオブジェクトが有効（表示）になったとき。") },
            { "OnDisable", ("オブジェクトが無効になったとき", "このオブジェクトが無効（非表示）になったとき。") },
            { "OnTriggerEnter", ("物体が範囲に入ったとき", "プレイヤー以外の物体（コライダー）が、このオブジェクトの範囲に入ったとき。") },
            { "OnTriggerExit", ("物体が範囲から出たとき", "プレイヤー以外の物体（コライダー）が、このオブジェクトの範囲から出たとき。") },
            { "UiButtonClick", ("ボタンが押されたとき", "UI のボタンが押されたとき。ボタンへの配線は Tripwire が自動で行います。") },
            { "UiToggleChanged", ("トグルが切り替わったとき", "UI のトグルが切り替わったとき。オン/オフを「イベントの値」として使えます。") },
            { "UiSliderChanged", ("スライダーが動いたとき", "UI のスライダーが動いたとき。位置（数値）を「イベントの値」として使えます。") },
            { "OnVideoReady", ("動画の準備ができたとき", "この動画プレイヤーで動画の読み込みが終わったとき。") },
            { "OnVideoStart", ("動画が始まったとき", "動画の再生が始まったとき。") },
            { "OnVideoEnd", ("動画が終わったとき", "動画の再生が終わったとき。最後まで再生したときと、操作で終えたときの両方です。") },
            { "OnVideoLoop", ("動画がループしたとき", "ループ再生で動画が最初に戻ったとき。") },
            { "OnVideoError", ("動画でエラーが起きたとき", "動画の読み込みや再生に失敗したとき。エラーの種類を「イベントの値」として使えます。") },
            { "ScriptNotified", ("ほかのスクリプトから通知されたとき", "ほかの UdonSharp スクリプト（ProTV などの動画プレイヤーやペンなど）から、決めた名前で通知が届いたとき。通知の登録は、ワールドの開始時に Tripwire が自動で行います。") },
            { "OnOwnershipTransferred", ("オーナーが変わったとき", "このオブジェクトのオーナーが別のプレイヤーに移ったとき。") },
            { "OnPlayerTriggerStay", ("プレイヤーが範囲にいる間", "プレイヤーがこのオブジェクトの範囲（トリガー）の中にいる間、物理演算のたびに（ふつう 1 秒に 50 回）。") },
            { "OnPlayerCollisionExit", ("プレイヤーが離れたとき", "ぶつかっていたプレイヤーが、このオブジェクトから離れたとき。") },
            { "OnPlayerCollisionStay", ("プレイヤーが触れている間", "プレイヤーがこのオブジェクトに触れている間、物理演算のたびに（ふつう 1 秒に 50 回）。") },
            { "OnPlayerParticleCollision", ("パーティクルがプレイヤーに当たったとき", "このパーティクルシステムの粒がプレイヤーに当たったとき（Collision の Send Collision Messages が必要）。") },
            { "OnControllerColliderHitPlayer", ("キャラクターコントローラーがプレイヤーにぶつかったとき", "このオブジェクトの CharacterController が、動いている途中でプレイヤーにぶつかったとき。") },
            { "OnTriggerStay", ("物体が範囲にいる間", "プレイヤー以外の物体（コライダー）が範囲の中にいる間、物理演算のたびに（ふつう 1 秒に 50 回）。") },
            { "OnCollisionEnter", ("物体がぶつかったとき", "プレイヤー以外の物体がこのオブジェクトにぶつかったとき（どちらかに Rigidbody が必要）。") },
            { "OnCollisionExit", ("物体が離れたとき", "ぶつかっていた物体が離れたとき。") },
            { "OnCollisionStay", ("物体が触れている間", "物体が触れている間、物理演算のたびに（ふつう 1 秒に 50 回）。") },
            { "OnTriggerEnter2D", ("2D の物体が範囲に入ったとき", "2D のコライダーが、この 2D の範囲（トリガー）に入ったとき。") },
            { "OnTriggerExit2D", ("2D の物体が範囲から出たとき", "2D のコライダーが、この 2D の範囲から出たとき。") },
            { "OnTriggerStay2D", ("2D の物体が範囲にいる間", "2D のコライダーが範囲の中にいる間、物理演算のたびに（ふつう 1 秒に 50 回）。") },
            { "OnCollisionEnter2D", ("2D の物体がぶつかったとき", "2D のコライダーがこのオブジェクトにぶつかったとき。") },
            { "OnCollisionExit2D", ("2D の物体が離れたとき", "ぶつかっていた 2D のコライダーが離れたとき。") },
            { "OnCollisionStay2D", ("2D の物体が触れている間", "2D のコライダーが触れている間、物理演算のたびに（ふつう 1 秒に 50 回）。") },
            { "OnControllerColliderHit", ("キャラクターコントローラーがぶつかったとき", "このオブジェクトの CharacterController が、動いている途中でコライダーにぶつかったとき。") },
            { "OnParticleCollision", ("パーティクルが当たったとき", "パーティクルがこのオブジェクトに当たったとき（またはこのパーティクルが何かに当たったとき）。") },
            { "OnParticleTrigger", ("パーティクルがトリガーに触れたとき", "このパーティクルシステムの Triggers モジュールの条件を満たしたとき。") },
            { "OnJointBreak", ("ジョイントが外れたとき", "このオブジェクトのジョイントが外れたとき。外れた力を「イベントの値」として使えます。") },
            { "OnJointBreak2D", ("2D のジョイントが外れたとき", "このオブジェクトの 2D ジョイントが外れたとき。") },
            { "OnTransformParentChanged", ("親が変わったとき", "このオブジェクトの親（ヒエラルキー上の親）が変わったとき。") },
            { "OnTransformChildrenChanged", ("子が増えた・減ったとき", "このオブジェクトの子が増えたり減ったりしたとき。") },
            { "LateUpdate", ("毎フレーム（Update の後）", "毎フレーム、すべての Update が終わった後に実行します。") },
            { "FixedUpdate", ("物理演算のたびに", "物理演算のたびに（ふつう 1 秒に 50 回）実行します。重い処理は入れないでください。") },
            { "PostLateUpdate", ("毎フレーム（プレイヤーが動いた後）", "毎フレーム、VRChat がプレイヤーの位置を更新した後に実行します（ボーンに追従させるときなど）。") },
            { "OnBecameVisible", ("画面に映ったとき", "このオブジェクトのレンダラーが、どれかのカメラに映るようになったとき。") },
            { "OnBecameInvisible", ("画面から消えたとき", "このオブジェクトのレンダラーが、どのカメラにも映らなくなったとき。") },
            { "OnWillRenderObject", ("描画される直前", "カメラがこのオブジェクトを描画する直前に、カメラごとに。") },
            { "OnPreCull", ("カメラが描画を始める前（カリング前）", "このオブジェクトのカメラが描画の準備（カリング）を始める前。") },
            { "OnPreRender", ("カメラが描画する前", "このオブジェクトのカメラが描画を始める前。") },
            { "OnPostRender", ("カメラが描画した後", "このオブジェクトのカメラが描画を終えた後。") },
            { "OnRenderObject", ("シーンの描画の後", "カメラがシーンを描画し終えた後。") },
            { "OnAnimatorMove", ("Animator がルートモーションを計算したとき", "このオブジェクトの Animator がルートモーションを計算したとき。") },
            { "OnAnimatorIK", ("Animator が IK を計算するとき", "このオブジェクトの Animator が IK を計算するとき。レイヤー番号を「イベントの値」として使えます。") },
            { "OnDestroy", ("オブジェクトが消されたとき", "このオブジェクトが削除（Destroy）されたとき。") },
            { "OnAsyncGpuReadbackComplete", ("GPU からの読み取りが終わったとき", "このスクリプトが頼んだ GPU からの読み取り（VRCAsyncGPUReadback）が終わったとき。") },
            { "OnVRCCameraSettingsChanged", ("カメラ設定が変わったとき", "プレイヤーのカメラの設定が変わったとき。") },
            { "OnVRCQualitySettingsChanged", ("画質設定が変わったとき", "プレイヤーの画質の設定が変わったとき。") },
            { "OnScreenUpdate", ("画面の向きが変わったとき", "モバイル端末で、ワールドに入ったときと、画面の向きが変わったとき。") },
            { "OnDeserialization", ("同期データを受け取ったとき", "オーナーから同期した変数が届いたとき（あとから来たときも届きます）。") },
            { "OnPreSerialization", ("同期データを送る直前", "このオブジェクトの同期した変数を送る直前（オーナーのみ）。") },
            { "OnPostSerialization", ("同期データを送った後", "同期した変数を送った後（オーナーのみ）。結果を「イベントの値」として使えます。") },
            { "OnMasterTransferred", ("マスターが変わったとき", "インスタンスのマスターが別のプレイヤーに移ったとき。") },
            { "OnPlayerRestored", ("プレイヤーの保存データが読み込まれたとき", "プレイヤーの保存データ（Persistence）が読み込まれたとき。") },
            { "OnPlayerDataUpdated", ("プレイヤーの保存データが変わったとき", "プレイヤーの保存データ（PlayerData）が変わったとき。") },
            { "OnPlayerDataStorageWarning", ("保存データ（PlayerData）が上限に近いとき", "プレイヤーの PlayerData が容量の上限に近づいたとき。") },
            { "OnPlayerDataStorageExceeded", ("保存データ（PlayerData）が上限を超えたとき", "プレイヤーの PlayerData が容量の上限を超えたとき。") },
            { "OnPlayerObjectStorageWarning", ("保存オブジェクトが上限に近いとき", "プレイヤーの保存オブジェクト（PlayerObject）が容量の上限に近づいたとき。") },
            { "OnPlayerObjectStorageExceeded", ("保存オブジェクトが上限を超えたとき", "プレイヤーの保存オブジェクトが容量の上限を超えたとき。") },
            { "OnPersistenceUsageUpdated", ("保存データの使用量が更新されたとき", "保存データの使用量の情報が更新されたとき。") },
            { "OnSpawn", ("オブジェクトプールから取り出されたとき", "このオブジェクトが VRC Object Pool から取り出されたとき。VRChat では非推奨で、代わりに「オブジェクトが有効になったとき」を使います。") },
            { "OnAvatarChanged", ("アバターを変えたとき", "プレイヤーがアバターを変えたとき。") },
            { "OnAvatarEyeHeightChanged", ("アバターの身長が変わったとき", "プレイヤーのアバターの目の高さが変わったとき。前の高さ（メートル）を「イベントの値」として使えます。") },
            { "OnPlayerSuspendChanged", ("プレイヤーのアプリが中断・再開したとき", "プレイヤーのアプリが一時停止したり再開したりしたとき（スマートフォンなど）。") },
            { "OnContactEnter", ("コンタクトが触れたとき", "このオブジェクトの VRC Contact Receiver に、コンタクト（手など）が触れたとき。") },
            { "OnContactExit", ("コンタクトが離れたとき", "このオブジェクトの VRC Contact Receiver から、コンタクトが離れたとき。") },
            { "OnPhysBoneGrabbed", ("PhysBone をつかんだとき", "このオブジェクトの PhysBone がつかまれたとき。") },
            { "OnPhysBoneReleased", ("PhysBone を離したとき", "つかまれていた PhysBone が離されたとき。") },
            { "OnPhysBonePosed", ("PhysBone がポーズされたとき", "このオブジェクトの PhysBone がポーズ（その形で固定）されたとき。") },
            { "OnPhysBoneUnPosed", ("PhysBone のポーズが解けたとき", "ポーズされていた PhysBone が元に戻されたとき。") },
            { "OnDroneTriggerEnter", ("ドローンが範囲に入ったとき", "プレイヤーのドローン（カメラドローン）が、このオブジェクトの範囲に入ったとき。") },
            { "OnDroneTriggerExit", ("ドローンが範囲から出たとき", "ドローンが、このオブジェクトの範囲から出たとき。") },
            { "OnDroneTriggerStay", ("ドローンが範囲にいる間", "ドローンが範囲の中にいる間、物理演算のたびに（ふつう 1 秒に 50 回）。") },
            { "InputJump", ("ジャンプボタンを押した・離したとき", "自分がジャンプボタンを押した・離したとき。押しているかを「イベントの値」として使えます。") },
            { "InputUse", ("Use ボタンを押した・離したとき", "自分が Use（トリガー）ボタンを押した・離したとき。押しているかを「イベントの値」として使えます。") },
            { "InputGrab", ("つかむボタンを押した・離したとき", "自分がつかむ（Grab）ボタンを押した・離したとき。") },
            { "InputDrop", ("離すボタンを押した・離したとき", "自分が離す（Drop）ボタンを押した・離したとき。") },
            { "InputMoveHorizontal", ("左右の移動入力が変わったとき", "自分の左右の移動入力が変わったとき（-1〜1）。") },
            { "InputMoveVertical", ("前後の移動入力が変わったとき", "自分の前後の移動入力が変わったとき（-1〜1）。") },
            { "InputLookHorizontal", ("左右の視点入力が変わったとき", "自分の左右の視点入力が変わったとき。") },
            { "InputLookVertical", ("上下の視点入力が変わったとき", "自分の上下の視点入力が変わったとき。") },
            { "OnInputMethodChanged", ("入力方法が変わったとき", "自分の入力方法（キーボード、コントローラー、タッチなど）が変わったとき。") },
            { "OnLanguageChanged", ("表示言語が変わったとき", "ワールドに入ったときと、自分が VRChat の表示言語を変えたとき。言語コードを「イベントの値」として使えます。") },
            { "MidiNoteOn", ("MIDI の鍵盤を押したとき", "MIDI の鍵盤が押されたとき（チャンネル、音の番号、強さ）。") },
            { "MidiNoteOff", ("MIDI の鍵盤を離したとき", "MIDI の鍵盤が離されたとき。") },
            { "MidiControlChange", ("MIDI のつまみを動かしたとき", "MIDI のつまみやスライダーが動いたとき（チャンネル、番号、値）。") },
            { "OnStringLoadSuccess", ("文字の読み込みが終わったとき", "VRCStringDownloader で頼んだ文字（Web 上のテキスト）が届いたとき。") },
            { "OnStringLoadError", ("文字の読み込みに失敗したとき", "VRCStringDownloader での読み込みに失敗したとき。") },
            { "OnImageLoadSuccess", ("画像の読み込みが終わったとき", "VRCImageDownloader で頼んだ画像が届いたとき。") },
            { "OnImageLoadError", ("画像の読み込みに失敗したとき", "VRCImageDownloader での読み込みに失敗したとき。") },
            { "OnVideoPlay", ("動画が再生されたとき", "動画が再生（一時停止からの再開を含む）されたとき。") },
            { "OnVideoPause", ("動画が一時停止したとき", "動画が一時停止されたとき。") },
            { "OnPurchaseConfirmed", ("購入が確認されたとき", "プレイヤーが商品を持っていることが確認されたとき（今買った場合も、前に買っていた場合も）。VRChat では非推奨で、代わりに「購入が確認されたとき（個数あり）」を使います。") },
            { "OnPurchaseConfirmedMultiple", ("購入が確認されたとき（個数あり）", "複数買える商品について、プレイヤーが持っている個数が確認されたとき。") },
            { "OnPurchaseExpired", ("購入の期限が切れたとき", "プレイヤーの商品の期限が切れたとき。") },
            { "OnProductEvent", ("商品のイベントが届いたとき", "商品を買った人が、その商品のイベント（Store.SendProductEvent）を送ったとき。インスタンスの全員に届きます。") },
            { "OnPurchasesLoaded", ("購入の一覧が読み込まれたとき", "プレイヤーの購入の一覧が読み込まれたとき。") },
            { "OnListPurchases", ("購入の一覧が届いたとき", "頼んだプレイヤーの購入の一覧が届いたとき。") },
            { "OnListAvailableProducts", ("商品の一覧が届いたとき", "このワールドで買える商品の一覧が届いたとき。") },
            { "OnListProductOwners", ("商品を持っている人の一覧が届いたとき", "商品を持っているプレイヤーの一覧（名前）が届いたとき。") },
            { "OnVRCPlusMassGift", ("VRC+ をまとめて贈ったとき", "プレイヤーが VRC+ をまとめて贈ったとき。贈った数を「イベントの値」として使えます。") },
        };

        public static string EventName(EventSpec s)
        {
            (string name, string desc) j;
            return Japanese && eventsJa.TryGetValue(s.Id, out j) ? j.name : s.DisplayName;
        }

        public static string EventDescription(EventSpec s)
        {
            (string name, string desc) j;
            return Japanese && eventsJa.TryGetValue(s.Id, out j) ? j.desc : s.Description;
        }

        /// <summary>What an event's value is called in the value pickers ("そのプレイヤー", "ぶつかった相手"...).</summary>
        public static string EventValueName(EventSpec spec, string name)
        {
            var m = spec?.Method ?? "";
            if (name == "other")
            {
                // What "other" is depends on the event: the one entering, leaving or staying; touching or colliding.
                bool collision = m.Contains("Collision");
                if (m.Contains("Enter")) return collision ? T("What hit it", "ぶつかった相手") : T("What entered", "入ってきた物");
                if (m.Contains("Exit")) return collision ? T("What left it", "離れた相手") : T("What left", "出ていった物");
                if (m.Contains("Stay")) return collision ? T("What touches it", "触れている相手") : T("What is inside", "中にいる物");
                return T("The other object", "相手のオブジェクト");
            }
            switch (name)
            {
                case "player": return T("That player", "そのプレイヤー");
                case "value":
                    // Buttons: pressed or not; movement and look: how far; UI: the toggle / slider value.
                    if (m == "InputJump" || m == "InputUse" || m == "InputGrab" || m == "InputDrop") return T("Pressed", "押しているか");
                    if (m.StartsWith("Input", StringComparison.Ordinal)) return T("How far (-1 to 1)", "入力の量（-1〜1）");
                    return T("The event's value", "イベントの値");
                case "newMaster": return T("The new master", "新しいマスター");
                case "gifter": return T("The giver", "贈った人");
                case "hit": return T("The hit", "ぶつかった情報");
                case "layerIndex": return T("Layer", "レイヤー番号");
                case "force": return T("Break force", "外れた力");
                case "joint": return T("The joint", "外れたジョイント");
                case "request": return T("The readback", "読み取り結果");
                case "cameraSettings": return T("Camera settings", "カメラ設定");
                case "data": return T("Screen data", "画面の情報");
                case "result": return T("The result", "結果");
                case "infos": return T("Changed entries", "変わった項目");
                case "prevEyeHeightAsMeters": return T("Previous eye height", "前の目の高さ");
                case "contactInfo": return T("The contact", "コンタクトの情報");
                case "physBoneInfo": return T("The PhysBone", "PhysBone の情報");
                case "drone": return T("The drone", "ドローン");
                case "args": return T("Input details", "入力の詳細");
                case "inputMethod": return T("Input method", "入力方法");
                case "language": return T("Language code", "言語コード");
                case "channel": return T("Channel", "チャンネル");
                case "number": return T("Note / control number", "番号");
                case "velocity": return T("Velocity", "強さ");
                case "product": return T("The product", "商品");
                case "products": return T("The products", "商品の一覧");
                case "purchasedNow": return T("Bought just now", "今買ったか");
                case "quantity": return T("Quantity", "個数");
                case "owners": return T("Owners", "持っている人");
                case "numGifts": return T("Number of gifts", "贈った数");
                case "videoError": return T("Error kind", "エラーの種類");
                default: return name;
            }
        }

        // ---- actions ----

        static readonly Dictionary<string, (string name, string desc)> actionsJa = new Dictionary<string, (string, string)>
        {
            { "GameObject.SetActive", ("オブジェクトを表示・非表示", "オブジェクトを有効（表示）または無効（非表示）にします。") },
            { "GameObject.ToggleActive", ("オブジェクトの表示を切り替える", "表示されていれば非表示に、非表示なら表示にします。") },
            { "Collider.SetEnabled", ("コライダーのオン・オフ", "見た目はそのままで、当たり判定だけを切り替えます。") },
            { "Renderer.SetEnabled", ("レンダラーのオン・オフ", "当たり判定は残して、見た目（描画）だけを切り替えます。") },
            { "Behaviour.SetEnabled", ("コンポーネントのオン・オフ", "ライトや音源などのコンポーネントを有効・無効にします。") },
            { "Transform.SetPosition", ("位置を変える", "オブジェクトを指定した位置へ動かします（自分の画面でだけ動きます）。") },
            { "Transform.MoveTo", ("別のオブジェクトの位置へ動かす", "オブジェクトを、指定したオブジェクトと同じ位置・向きへ動かします（自分の画面でだけ動きます）。") },
            { "Animator.SetTrigger", ("Animator の Trigger を送る", "Animator の Trigger パラメータを送ります。") },
            { "Animator.SetBool", ("Animator の Bool を変える", "Animator の Bool パラメータを変えます。") },
            { "Animator.SetInteger", ("Animator の Int を変える", "Animator の Int パラメータを変えます。") },
            { "Animator.SetFloat", ("Animator の Float を変える", "Animator の Float パラメータを変えます。") },
            { "Animator.Play", ("Animator のステートを再生", "ステート名を指定して、そこから再生します。") },
            { "AudioSource.Play", ("音を鳴らす", "音源を最初から再生します。") },
            { "AudioSource.Stop", ("音を止める", "音源を止めます。") },
            { "AudioSource.PlayOneShot", ("効果音を 1 回鳴らす", "鳴っている音を止めずに、別の音を 1 回だけ重ねて鳴らします。") },
            { "ParticleSystem.Play", ("パーティクルを出す", "パーティクルを再生します。") },
            { "ParticleSystem.Stop", ("パーティクルを止める", "パーティクルを止めます。") },
            { "Text.SetText", ("テキストを変える", "TextMeshPro のテキストを書き換えます。{変数名} で変数の値を入れられます。") },
            { "Player.Teleport", ("テレポートする", "自分を、指定したオブジェクトの位置・向きへ移動させます。") },
            { "Player.SetSpeed", ("移動の速さを変える", "自分の歩く・走る・横歩きの速さと、ジャンプの強さを変えます。") },
            { "Pickup.Drop", ("ピックアップを離させる", "自分が持っているピックアップを手から離させます。") },
            { "Networking.TakeOwnership", ("オーナーになる", "自分をこのオブジェクトのオーナーにします。") },
            { "Video.PlayUrl", ("URL の動画を再生", "URL を読み込んで再生します（新しい URL を読めるのは 5 秒に 1 回まで）。") },
            { "Video.LoadUrl", ("URL の動画を読み込む", "URL を読み込むだけで、まだ再生しません。") },
            { "Video.Play", ("動画を再生", "読み込んだ動画を再生（一時停止からの再開）します。") },
            { "Video.Pause", ("動画を一時停止", "動画を一時停止します。") },
            { "Video.Stop", ("動画を止める", "動画を停止します。") },
            { "Video.SetTime", ("動画の位置を変える", "指定した秒数の位置へ移動します。") },
            { "Video.SetLoop", ("動画のループ", "ループ再生のオン・オフを切り替えます。") },
            { "Flow.Repeat", ("Repeat", "決めた回数だけ、中のアクション（Do）をくり返します。何回目か（0 から）を変数に入れることもできます。") },
            { "Flow.ForEach", ("For Each", "リストの変数の中身を 1 つずつ変数に入れて、中のアクション（Do）を実行します。") },
            { "Flow.While", ("While", "条件を満たしている間、中のアクション（Do）をくり返します。中で条件の変数を変えないとループが終わらず、Udon がこのトリガーを止め、以後動かなくなります。") },
            { "Flow.Continue", ("Continue", "ループの今の回の残りを飛ばして、次の回へ進みます。") },
            { "Flow.Break", ("Break", "ループをやめて、その後ろへ進みます。") },
            { "Flow.Stop", ("Return", "このイベントの残りのアクションを実行しません。") },
            { "Flow.If", ("If", "条件を満たすとき（Then）と満たさないとき（Else）で、別のアクションを実行します。Else に If を入れると Else If になります。") },
            { "Variable.Random", ("ランダムな数を入れる", "最小〜最大のランダムな数を、数の変数に入れます（整数なら最大も含む）。") },
            { "Timer.Start", ("タイマーを始める", "このトリガーのタイマーを始めます（動いていれば始め直します）。") },
            { "Timer.Stop", ("タイマーを止める", "このトリガーのタイマーを止めます。") },
            { "Variable.Set", ("変数に値を入れる", "変数の値を変えます。同期した変数なら全員に届きます。") },
            { "Variable.Toggle", ("変数を切り替える", "オン/オフの変数のオンとオフを入れ替えます。") },
            { "Variable.Add", ("変数に足す", "数の変数に値を足します（引くときはマイナス）。") },
            { "Variable.GetComponent", ("コンポーネントを取り出す", "オブジェクト（またはその子や親）のコンポーネントを、オブジェクトの変数に入れます。どのコンポーネントかは、変数の型で決まります。") },
            { "Variable.Calculate", ("計算して変数に入れる", "A と B を + − × ÷ %（余り）で計算して、変数に入れます。数・位置・色を計算でき、文字は + でつなげられます。") },
            { "Trigger.SetVariable", ("ほかのトリガーの変数を変える", "ほかのトリガーの変数を変えます。その変数が同期していれば全員に届き、「変数が変わったとき」も動きます。続けて「カスタムイベントを呼ぶ」と、値を渡して呼べます。") },
            { "Trigger.GetVariable", ("ほかのトリガーの変数を読む", "ほかのトリガーの変数の値を、このトリガーの変数に入れます。") },
            { "Event.Send", ("カスタムイベントを呼ぶ", "トリガーやスクリプトのカスタムイベントを、名前で呼び出します。") },
            { "Event.SendDelayed", ("何秒か後にカスタムイベントを呼ぶ", "何秒か後に、カスタムイベントを名前で呼び出します。") },
            { "Script.Call", ("ほかのスクリプトを使う", "ほかの UdonSharp スクリプト（ギミックや動画プレイヤーなど）の機能を呼んだり、値を変えたりします。") },
            { "Udon.Call", ("Udon の機能を呼ぶ（Udon API）", "Udon で使えるメソッドやプロパティを、検索して何でも呼べます。") },
            { "Debug.Log", ("ログを出す", "Unity のコンソールにメッセージを出します（確認用）。") },
        };

        public static string ActionName(ActionSpec s)
        {
            (string name, string desc) j;
            return Japanese && actionsJa.TryGetValue(s.Id, out j) ? j.name : s.DisplayName;
        }

        public static string ActionDescription(ActionSpec s)
        {
            (string name, string desc) j;
            return Japanese && actionsJa.TryGetValue(s.Id, out j) ? j.desc : s.Description;
        }

        // ---- parameter labels ----

        static readonly Dictionary<string, (string en, string ja)> paramsText = new Dictionary<string, (string, string)>
        {
            { "targets", ("Targets", "対象") },
            { "active", ("Show", "表示する") },
            { "enabled", ("On", "オンにする") },
            { "position", ("Position", "位置") },
            { "destination", ("Destination", "移動先") },
            { "parameter", ("Parameter", "パラメータ名") },
            { "value", ("Value", "値") },
            { "state", ("State", "ステート名") },
            { "clip", ("Sound", "鳴らす音") },
            { "text", ("Text", "文字") },
            { "walk", ("Walk", "歩く速さ") },
            { "run", ("Run", "走る速さ") },
            { "strafe", ("Strafe", "横歩きの速さ") },
            { "jump", ("Jump", "ジャンプの強さ") },
            { "event", ("Event name", "呼ぶ名前") },
            { "broadcast", ("Run on", "誰の画面で") },
            { "seconds", ("Seconds", "何秒後") },
            { "message", ("Message", "メッセージ") },
            { "url", ("URL", "URL") },
            { "loop", ("Loop", "ループする") },
            { "variable", ("Variable", "変数") },
            { "amount", ("Amount", "足す量") },
            { "a", ("A", "A") },
            { "b", ("B", "B") },
            { "operator", ("Operator", "計算") },
            { "source", ("Object", "取り出すもと") },
            { "where", ("Look in", "探す場所") },
            { "min", ("Min", "最小") },
            { "max", ("Max", "最大") },
            { "timer", ("Timer", "タイマー") },
            { "trigger", ("Trigger", "ほかのトリガー") },
            { "remoteVariable", ("Its variable", "そのトリガーの変数") },
            { "into", ("Into", "入れる変数") },
            { "count", ("Times", "回数") },
            { "counter", ("Round into", "何回目か") },
            { "list", ("List", "リスト") },
            { "item", ("Each item into", "中身を入れる変数") },
        };

        public static string Param(string name)
        {
            (string en, string ja) p;
            return paramsText.TryGetValue(name ?? "", out p) ? T(p.en, p.ja) : name;
        }

        public static readonly string[] BroadcastChoicesJa = { "自分だけ（Local）", "全員（All）", "オーナーだけ（Owner）" };
        public static readonly string[] BroadcastChoicesEn = { "Only my screen", "Everyone's screen", "Owner's screen" };
        public static string[] BroadcastChoices => Japanese ? BroadcastChoicesJa : BroadcastChoicesEn;
        public static string[] WhereChoices => Japanese ? new[] { "このオブジェクト", "子も探す", "親も探す" } : new[] { "This object", "Children too", "Parents too" };

        // ---- type names ----

        /// <summary>A type as users think of it: 整数 / オブジェクトのリスト / Animator.</summary>
        public static string TypeName(ParamType t)
        {
            if (t == null) return "?";
            string one;
            switch (t.Kind)
            {
                case ValueKind.Bool: one = T("On/Off", "オン/オフ"); break;
                case ValueKind.Int: one = T("Integer", "整数"); break;
                case ValueKind.Float: one = T("Number", "小数"); break;
                case ValueKind.String: one = T("Text", "文字"); break;
                case ValueKind.Vector3: one = T("Position", "位置"); break;
                case ValueKind.Vector2: one = "Vector2"; break;
                case ValueKind.Color: one = T("Color", "色"); break;
                case ValueKind.Quaternion: one = T("Rotation", "回転"); break;
                case ValueKind.Player: one = T("Player", "プレイヤー"); break;
                case ValueKind.Url: one = "URL"; break;
                case ValueKind.Object:
                    one = t.UnityType == "UnityEngine.GameObject" ? T("Object", "オブジェクト") : Short(t.UnityType);
                    break;
                default:
                    // Lists of basic values (int[], string[]...) are "Other" types: name them by their items.
                    if (t.UnityType != null && t.UnityType.EndsWith("[]", StringComparison.Ordinal) && CodeGenerator.ElementTypeOf(t) is ParamType item && item.Kind != ValueKind.Other)
                        return Japanese ? TypeName(item) + "のリスト" : TypeName(item) + " list";
                    return Short(t.UnityType);
            }
            if (!t.IsArray) return one;
            return Japanese ? one + "のリスト" : one + " list";
        }

        /// <summary>What the event is called in Udon / Unity, for people who know those names.</summary>
        public static string EventCode(EventSpec e)
        {
            switch (e.Id)
            {
                case EventCatalog.CustomId: return "Custom Event";
                case EventCatalog.VariableChangedId: return "OnVariableChanged";
                case "UiButtonClick": return "Button.onClick";
                case "UiToggleChanged": return "Toggle.onValueChanged";
                case "UiSliderChanged": return "Slider.onValueChanged";
                case EventCatalog.ScriptNotifiedId: return "SendCustomEvent";
                default: return e.Method ?? e.Id;
            }
        }

        /// <summary>What the action is called in Udon / Unity.</summary>
        public static string ActionCode(string id)
        {
            switch (id)
            {
                case ActionCatalog.CallId: return "Udon API";
                case ActionCatalog.ScriptCallId: return "UdonSharp";
                case ActionCatalog.SendEventId: return "SendCustomEvent";
                case ActionCatalog.SendEventDelayedId: return "SendCustomEventDelayedSeconds";
                default: return id;
            }
        }

        /// <summary>What a value of this type holds, in plain words (the type popup's hover text).</summary>
        public static string TypeDescription(ParamType t)
        {
            if (t == null) return "";
            if (t.IsArray || (t.UnityType != null && t.UnityType.EndsWith("[]", StringComparison.Ordinal)))
                return T("Holds several values of one kind, in order.", "同じ種類の値を、いくつも順に持ちます。");
            switch (t.Kind)
            {
                case ValueKind.Bool: return T("Holds on or off (a door is open, a light is lit...).", "オンかオフのどちらかを入れられます（扉が開いている、ライトがついている など）。");
                case ValueKind.Int: return T("Holds a whole number (counts, scores...).", "整数を入れられます（回数、点数 など）。");
                case ValueKind.Float: return T("Holds a number with decimals (time, volume...).", "小数を入れられます（時間、音量 など）。");
                case ValueKind.String: return T("Holds text (names, messages...).", "文字を入れられます（名前、メッセージ など）。");
                case ValueKind.Vector3: return T("Holds a position or direction (x, y, z).", "位置や向き（x, y, z）を入れられます。");
                case ValueKind.Vector2: return T("Holds two numbers (x, y).", "2 つの数（x, y）を入れられます。");
                case ValueKind.Color: return T("Holds a color.", "色を入れられます。");
                case ValueKind.Quaternion: return T("Holds a rotation.", "回転を入れられます。");
                case ValueKind.Player: return T("Holds one player.", "プレイヤーを 1 人持ちます。");
                case ValueKind.Url: return T("Holds a URL (video and loading actions take it).", "URL を入れられます（動画や読み込みのアクションで使います）。");
                case ValueKind.Object:
                    return t.UnityType == "UnityEngine.GameObject"
                        ? T("Holds one object of the scene.", "シーンのオブジェクトを 1 つ持ちます。")
                        : T("Holds one " + Short(t.UnityType) + " of the scene.", "シーンの " + Short(t.UnityType) + " を 1 つ持ちます。");
                default:
                    return T("Holds a " + Short(t.UnityType) + ".", Short(t.UnityType) + " を入れられます。");
            }
        }

        static string Short(string typeName) => string.IsNullOrEmpty(typeName) ? "?" : typeName.Substring(typeName.LastIndexOf('.') + 1);
    }
}
