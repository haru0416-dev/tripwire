# 動画プレイヤー

Tripwire は、VRChat の動画プレイヤーと、ProTV、VizVid、USharpVideo、VideoTXL を扱えます。

## VRChat の動画プレイヤー

VRC Unity Video Player や VRC AVPro Video Player は、「動画プレイヤー」の分類のアクションで操作します。URL の再生、一時停止、停止、位置の移動、ループの切り替えがあります。

<TriggerCard event="Interact"
  :actions="[{ id: 'Video.PlayUrl', rows: [{ p: 0, value: 'Screen', kind: 'object' }, { p: 1, value: 'https://…', kind: 'text' }] }]" />

::: warning URL は 5 秒に 1 回まで
VRChat では、新しい URL を読み込めるのは 5 秒に 1 回までです。
:::

再生が始まった、終わったといったイベントは、「動画プレイヤー」の分類のイベントで受け取ります。これらのイベントは、動画プレイヤーと同じオブジェクトに付けたトリガーにしか届きません。

## ProTV・VizVid・USharpVideo・VideoTXL

これらは、プレイヤーのスクリプトから通知を受け取ります。

<div class="steps">

1. 「いつ」に「ほかのスクリプトから通知されたとき」を選びます
2. 「通知元」に、プレイヤーのオブジェクト（ProTV なら TVManager の付いたもの）を入れます
3. 「何が起きたとき」から、「再生されたとき」「動画が最後まで再生されたとき」などを選びます

</div>

<TriggerCard event="ScriptNotified" :rows="[{ label: { ja: '通知元', en: 'Notified by' }, value: 'TVManager', kind: 'object' }, { label: { ja: '何が起きたとき', en: 'What happened' }, value: { ja: '再生されたとき', en: 'Started playing' } }]"
  :actions="[{ id: 'GameObject.SetActive', rows: [{ p: 0, value: 'Room Lights', kind: 'object' }, { p: 1, value: { ja: 'オフ', en: 'Off' } }] }]" />

操作は、「ほかのスクリプトを使う」で対象にプレイヤーのオブジェクトを入れ、「使うもの」から「URL の動画を再生」などを選びます。

::: info 対応していないもの
YamaPlayer と QvPen は、通知を受け取るのに専用のしくみが必要なので、まだ対応していません。iwaSync3 は、現行版のソースが公開されていないので、一覧に入れていません。
:::
