Store.ListProductOwners を呼んだとき、その答えとして起きます。「持っている人」は、その商品を持っている人の表示名の一覧です。今いるインスタンスの人だけでなく、商品を持っているすべての人が入ります。

VRChat.com でその商品の「Owners Names in Udon」の設定をオンにしておかないと、このイベントは起きません。手元で試すときは、本当の名前の代わりに VRCat・Fred・VRRat という仮の名前が入ります。同じオブジェクトに UdonBehaviour が複数あると、うまく動かないことがあります。

Tripwire には、ListProductOwners を呼ぶアクションがありません。このイベントは、ほかの UdonSharp スクリプトがこのトリガーを受け取り先にして呼んだときに起きます。

公式の説明: [Udon Documentation（VRChat）](https://creators.vrchat.com/economy/sdk/udon-documentation/)
