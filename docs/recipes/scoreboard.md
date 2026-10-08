# スコアボード

ボタンを押すたびに点数が増え、看板に出るスコアボードです。点数は全員で共有し、あとから来た人にも届きます。

## 用意するもの

- ボタンにするオブジェクト（コライダー付き）
- 点数を出す看板（TextMeshPro）

## 変数

| 名前 | 種類 | 同期 |
|---|---|---|
| スコア | 整数 | 同期する |

## カード

<TriggerCard event="Interact"
  :actions="[{ id: 'Variable.Add', rows: [{ p: 0, value: { ja: 'スコア', en: 'score' } }, { p: 1, value: '1', kind: 'number' }] }]" />

<TriggerCard event="OnVariableChanged" :name="{ ja: 'スコア', en: 'score' }"
  :actions="[{ id: 'Text.SetText', rows: [{ p: 0, value: 'Score Board', kind: 'object' }, { p: 1, value: { ja: 'スコア: {スコア}点', en: 'Score: {score}' }, kind: 'text' }] }]" />

文字の中の `{スコア}` は、変数の今の値に置き換わります。

## 変えてみる

- 0 に戻すボタン: 別のボタンで「変数に値を入れる」を使い、スコアを 0 にします
- 減らすボタン: 「変数に足す」の足す量を -1 にします
