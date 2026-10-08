# テレポート

クリックすると、決めた場所へ移動するボタンです。

## 用意するもの

- ボタンにするオブジェクト（コライダー付き）
- 移動先にする空のオブジェクト（位置と向きを合わせておく）

## カード

<TriggerCard event="Interact"
  :actions="[{ id: 'Player.Teleport', rows: [{ p: 0, value: 'Teleport Point', kind: 'object' }] }]" />

テレポートで動くのは、押した本人だけです。

## 範囲に入ったら移動する

ボタンの代わりに、範囲に入った人を移動させることもできます。

<TriggerCard event="OnPlayerTriggerEnter" :rows="[{ label: { ja: '誰が入ったら', en: 'Who enters' }, value: { ja: '自分', en: 'Me' } }]"
  :actions="[{ id: 'Player.Teleport', rows: [{ p: 0, value: 'Teleport Point', kind: 'object' }] }]" />

範囲にするオブジェクトには、Is Trigger をオンにしたコライダーが要ります。ないときはカードに「範囲を付ける」ボタンが出ます。

カードの「誰が入ったら」は「自分」にしてください。「誰でも」にすると、ほかの人が範囲に入ったときに、自分の画面でもテレポートが動いて自分まで移動します。
