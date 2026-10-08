# サイコロ

押すと 1〜6 の目が出て、看板とアニメーションで見せます。出た目は全員で共有します。

## 用意するもの

- 振るボタン（コライダー付き）
- 目を出す看板（TextMeshPro）
- 出た目ごとのアニメーションを持つ Animator（Int のパラメータ Face で切り替える）

## 変数

| 名前 | 種類 | 同期 |
|---|---|---|
| 出た目 | 整数 | 同期する |

## カード

<TriggerCard event="Interact"
  :actions="[{ id: 'Variable.Random', rows: [{ p: 0, value: { ja: '出た目', en: 'face' } }, { p: 1, value: '1', kind: 'number' }, { p: 2, value: '6', kind: 'number' }] }]" />

<TriggerCard event="OnVariableChanged" :name="{ ja: '出た目', en: 'face' }"
  :actions="[
    { id: 'Text.SetText', rows: [{ p: 0, value: 'Dice Sign', kind: 'object' }, { p: 1, value: '{出た目}', kind: 'text' }] },
    { id: 'Animator.SetInteger', rows: [{ p: 0, value: 'Dice', kind: 'object' }, { p: 1, value: 'Face', kind: 'text' }, { p: 2, value: { ja: '変数: 出た目', en: 'Variable: face' } }] },
  ]" />

「ランダムな数を入れる」は、整数の変数なら最大の値（6）も出ます。

::: tip 同じ目が続いたとき
前と同じ目が出ると、変数の値は変わらないので「変数が変わったとき」は動きません。毎回アニメーションを流したいときは、1 枚目にも同じアクションを置くか、振った回数を数える同期する変数を足して、そちらで受けてください。
:::
