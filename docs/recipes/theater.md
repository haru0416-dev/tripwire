# シアター

ボタンごとに決めた動画を流し、再生が始まったら照明を消します。VRChat の動画プレイヤーと ProTV の両方の例を載せます。

## VRChat の動画プレイヤーで

### 再生ボタン

ボタンごとに Tripwire Trigger を付けます。

<TriggerCard event="Interact"
  :actions="[{ id: 'Video.PlayUrl', rows: [{ p: 0, value: 'Screen', kind: 'object' }, { p: 1, value: 'https://…', kind: 'text' }] }]" />

::: warning 全員で見るとき
VRChat の動画プレイヤーは、再生を自分では同期しません。上のカードのままだと、押した人の画面でだけ再生されます。全員で見るときは、カードの詳細設定で「全員（All）」にします。ただし、あとから来た人には届きません。あとから来た人にもそろえたいときは、同期のしくみを持つ ProTV などを使うのが確実です。
:::

### 照明を消す

動画プレイヤー（Screen）と同じオブジェクトに Tripwire Trigger を付けます。動画のイベントは、同じオブジェクトのトリガーにしか届かないからです。

<TriggerCard event="OnVideoStart"
  :actions="[{ id: 'GameObject.SetActive', rows: [{ p: 0, value: 'Room Lights', kind: 'object' }, { p: 1, value: { ja: 'オフ', en: 'Off' } }] }]" />

<TriggerCard event="OnVideoEnd"
  :actions="[{ id: 'GameObject.SetActive', rows: [{ p: 0, value: 'Room Lights', kind: 'object' }, { p: 1, value: { ja: 'オン', en: 'On' } }] }]" />

## ProTV で

ProTV では、再生ボタンに「ほかのスクリプトを使う」を置き、対象に TVManager の付いたオブジェクトを入れて、「使うもの」から「URL の動画を再生」を選びます。

照明は、どのオブジェクトのトリガーでも受けられます。

<TriggerCard event="ScriptNotified" :rows="[{ label: { ja: '通知元', en: 'Notified by' }, value: 'TVManager', kind: 'object' }, { label: { ja: '何が起きたとき', en: 'What happened' }, value: { ja: '再生されたとき', en: 'Started playing' } }]"
  :actions="[{ id: 'GameObject.SetActive', rows: [{ p: 0, value: 'Room Lights', kind: 'object' }, { p: 1, value: { ja: 'オフ', en: 'Off' } }] }]" />

::: info 照明を全員でそろえる
動画の再生は、ProTV などのプレイヤーが全員の画面で同期します。照明はそれぞれの画面で再生の通知を受けて落とすので、同期する変数を使わなくても全員でそろいます。
:::
