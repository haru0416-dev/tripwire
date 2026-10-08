# 鍵の扉

鍵を拾った人だけが開けられる扉です。鍵を持っていない人がクリックすると、看板に「鍵が必要です」と出ます。

## 用意するもの

- 鍵（VRC Pickup の付いたオブジェクト）
- 扉（クリックするオブジェクト）と、その中で消す扉の板
- メッセージを出す看板（TextMeshPro）

## 扉のトリガー

扉に Tripwire Trigger を付け、変数を作ります。

| 名前 | 種類 | 同期 |
|---|---|---|
| 鍵を持っている | オン/オフ | 同期しない |

<TriggerCard event="Interact"
  :actions="[{ id: 'Flow.If', rows: [{ label: { ja: '条件', en: 'Condition' }, value: { ja: '鍵を持っている が オン のとき', en: 'has key is On' }, kind: 'text' }],
    then: [{ id: 'GameObject.SetActive', rows: [{ p: 0, value: 'Door Panel', kind: 'object' }, { p: 1, value: { ja: 'オフ', en: 'Off' } }] }],
    else: [{ id: 'Text.SetText', rows: [{ p: 0, value: 'Sign', kind: 'object' }, { p: 1, value: { ja: '鍵が必要です', en: 'You need the key' }, kind: 'text' }] }] }]" />

## 鍵のトリガー

鍵に Tripwire Trigger を付け、「ピックアップを持ったとき」に扉の変数を変えます。

<TriggerCard event="OnPickup"
  :actions="[{ id: 'Trigger.SetVariable', rows: [{ p: 0, value: 'Door', kind: 'object' }, { p: 1, value: { ja: '鍵を持っている', en: 'has key' } }, { p: 2, value: { ja: 'オン', en: 'On' } }] }]" />

変数は同期しないので、鍵を拾った人の画面でだけオンになります。扉が開くのも、その人の画面だけです。

## 全員の画面で開けたいとき

「鍵を持っている」の判定はそのままにして、扉の状態を別の同期する変数に持たせます。Then で同期する変数をオンにし、「変数が変わったとき」で扉の板を消します。形は [全員で共有するスイッチ](./shared-switch) と同じです。
