# 点滅するライト

3〜6 秒のランダムな間隔で、ライトがついたり消えたりします。スイッチを押すと止まります。

## 用意するもの

- 点滅させるライト
- 止めるスイッチ（コライダー付き）

## カード

ライトとは別のオブジェクト（スイッチなど）に Tripwire Trigger を付けます。ライト自身に付けると、ライトを消したときにトリガーも止まるからです。

<TriggerCard event="Timer" :name="{ ja: '点滅', en: 'blink' }"
  :rows="[{ label: { ja: '間隔', en: 'Every' }, value: { ja: '3 〜 6 秒（ランダム）', en: '3 – 6 seconds (random)' }, kind: 'text' }, { label: { ja: '実行', en: 'Runs' }, value: { ja: 'くり返す', en: 'Repeatedly' } }]"
  :actions="[{ id: 'GameObject.ToggleActive', rows: [{ p: 0, value: 'Lamp', kind: 'object' }] }]" />

<TriggerCard event="Interact"
  :actions="[{ id: 'Timer.Stop', rows: [{ p: 0, value: { ja: '点滅', en: 'blink' } }] }]" />

タイマーは各プレイヤーの画面でそれぞれ動くので、点滅のタイミングは人によってずれます。

## 変えてみる

- もう一度押すと再開する: 2 枚目を If にして、同期しない変数で動いているかを持ち、「タイマーを始める」と「タイマーを止める」を分けます
- 一定の間隔にする: 「ランダム」を外します
