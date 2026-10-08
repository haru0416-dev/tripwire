プレイヤーの購入が読み込まれて確認できたときに起きます。自分がインスタンスに入ったとき（自分とその場の全員の分）、ほかの人が入ってきたとき、誰かがその場でワールドの商品を買ったときです。「今買ったか」は、その場で買ったときにオン、入ったときの読み込みならオフです。

「個数」は買った数です。個数を選んで買える Instant の出品なら 1〜99 で、それ以外の出品ではいつも 1 です。古い「購入が確認されたとき」と同じトリガーに置くと、同じ購入を 2 回受け取ることがあります。

商品のイベントが届くのは、ワールドで使われている商品だけです。どれかの UdonBehaviour から UdonProduct を一度は参照した状態でアップロードしてください。無効なオブジェクトや UdonBehaviour では、ほとんどの購入のイベントが起きません。

試すときは、ClientSim と UdonProducts Manager で購入や期限切れを起こせます。Build & Test では出品は無料で、買ったものは 60 秒で期限が切れます。カードの「誰のとき」は最初「自分」です。

公式の説明: [Udon Documentation（VRChat）](https://creators.vrchat.com/economy/sdk/udon-documentation/)、[Testing Udon Products（VRChat）](https://creators.vrchat.com/economy/sdk/testing/)
