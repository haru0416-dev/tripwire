「プレイヤーが範囲に入ったとき」と同じく、全員の画面でそれぞれ起き、「誰が出たら」で自分・自分以外・誰でもを選びます。範囲に入ったときに付けた演出を、出たときに戻すのによく使います。

範囲は Is Trigger をオンにしたコライダーで、出たかどうかはプレイヤーのカプセル形のコライダーで判定されます。公式の説明によると、テレポートで範囲から出たときやとても速く動いたときは、このイベントが起きないことがあります。範囲の中から別の場所へテレポートさせる仕掛けでは、出たときの戻す処理が動かない場合があるので気をつけてください。

公式の説明: [Player Collisions（VRChat）](https://creators.vrchat.com/worlds/udon/players/player-collisions/)、[Event Nodes（VRChat）](https://creators.vrchat.com/worlds/udon/graph/event-nodes/)
