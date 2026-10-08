プレイヤーのカプセル形のコライダーが、このオブジェクトのコライダー（Is Trigger がオフのもの）に当たったときに起きます。インスタンスにいる誰が当たっても起きるので、カードの「誰のとき」で自分・自分以外・誰でもを選びます。

公式の説明では、プレイヤーが止まっている物に歩いてぶつかっても、このイベントは起きません。ボールや弾のように、動いてくる物がプレイヤーに当たったときのためのイベントです。プレイヤーが壁などに近づいたことを知りたいときは、範囲と「[プレイヤーが範囲に入ったとき](/reference/events/onplayertriggerenter)」を使います。

Walkthrough レイヤーと Pickup レイヤーのコライダーは、プレイヤーとぶつかりません。このレイヤーに置いた物では、当たったことになりません。

公式の説明: [Player Collisions（VRChat）](https://creators.vrchat.com/worlds/udon/players/player-collisions/)、[Event Nodes（VRChat）](https://creators.vrchat.com/worlds/udon/graph/event-nodes/)、[Layers（VRChat）](https://creators.vrchat.com/worlds/layers/)
