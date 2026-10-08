このイベントは公式の説明で非推奨になっていて、個数のある購入にも対応していません。新しく作るときは「購入が確認されたとき（個数あり）」を使ってください（[OnPurchaseConfirmedMultiple](/reference/events/onpurchaseconfirmedmultiple)）。両方を同じトリガーに置くと、同じ購入を 2 回受け取ることがあります。

起きるのは、購入が読み込まれて確認できたときです。自分がインスタンスに入ったとき（自分とその場の全員の分）、ほかの人が入ってきたとき、誰かがその場でワールドの商品を買ったときに読み込まれます。「今買ったか」は、その場で買ったときにオン、入ったときの読み込みならオフです。

カードの「誰のとき」は最初「自分」なので、自分の購入のときだけ動きます。

公式の説明: [Udon Documentation（VRChat）](https://creators.vrchat.com/economy/sdk/udon-documentation/)
