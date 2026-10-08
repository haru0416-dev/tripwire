# Tripwire とは

Tripwire は、VRChat ワールドのギミックを Inspector で組み立てる Unity エディタ拡張です。オブジェクトに付けた Tripwire Trigger に、「いつ（イベント）」と「何をする（アクション）」を一覧から選んで並べます。並べた内容は UdonSharp のスクリプトに変換され、そのまま Udon で動きます。

<TriggerCard event="OnPlayerTriggerEnter"
  :actions="[{ id: 'AudioSource.Play', rows: [{ p: 0, value: 'Chime', kind: 'object' }] }]" />

上のカードは、「プレイヤーが範囲に入ったら、Chime の音を鳴らす」という 1 枚です。カードを何枚も並べ、カードの中にアクションを何個も並べてギミックを作ります。

## できること

- いつ: クリック、範囲への出入り、ピックアップ、プレイヤーの参加、UI のボタン、動画の再生など、UdonSharp で使えるイベントのほぼすべて
- 何をする: 表示の切り替え、移動、Animator、音、文字の書き換え、テレポート、動画プレイヤーの操作、変数の変更など
- 流れ: If での分岐、Repeat・For Each・While でのくり返し、一定時間ごとに動くタイマー
- 変数と同期: オン/オフ、数、文字、オブジェクトなどの値を持ち、「同期する」にするとあとから来た人にも届く

一覧にない操作も、Udon から使えるメソッドを検索して呼べます。ProTV、VizVid、USharpVideo、VideoTXL は、再生や停止の通知を受け取るイベントと、よく使う操作を一覧から選べます。

全部の一覧は [イベント](/reference/events) と [アクション](/reference/actions) にあります。

## CyanTrigger との関係

プログラムを書かずにギミックを作れた CyanTrigger が更新されなくなったので、その代わりになるものとして作り始めました。CyanTrigger とは別に一から書いたもので、CyanTrigger のコードは使っていません。CyanTrigger で作ったトリガーを読み込む機能もありません。

## 生成されるもの

変換したスクリプトは、ふつうの UdonSharp のコードです。VRChat に Tripwire 専用のしくみを入れる必要はなく、ワールドを見る人にも何も要りません。スクリプトは `Assets/TripwireGenerated` に置かれ、必要なときに作り直されます。
