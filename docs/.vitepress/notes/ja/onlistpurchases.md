自分の画面で Store.ListPurchases を呼んだとき、その答えとして起きます。「商品の一覧」は、指定したプレイヤーが買った商品の一覧です。

Tripwire には、ListPurchases を呼ぶアクションがありません。このイベントは、ほかの UdonSharp スクリプトがこのトリガーを受け取り先にして呼んだときに起きます。入ってきた人の購入を知るだけなら、「購入の一覧が読み込まれたとき」が自動で起きます（[OnPurchasesLoaded](/reference/events/onpurchasesloaded)）。

カードの「誰のとき」は最初「自分」で、一覧を頼んだ相手が自分のときだけ動きます。

公式の説明: [Udon Documentation（VRChat）](https://creators.vrchat.com/economy/sdk/udon-documentation/)
