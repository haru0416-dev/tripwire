このパーティクルシステムの粒が、プレイヤーのカプセルに当たったときに起きます。誰に当たっても起きるので、カードの「誰のとき」で自分・自分以外・誰でもを選びます。自分に当たったときだけ演出を出すなら「自分」です。

起きるには、パーティクルシステムの Collision モジュールをオンにし、その中の Send Collision Messages もオンにします。VRChat の例では、Collision の種類を World にしています。粒が当たるのは、Collision モジュールの Collides With で選んだレイヤーの物だけです。

ほかの物に当たったことは「[パーティクルが当たったとき](/reference/events/onparticlecollision)」で受けます。

公式の説明: [Player Collisions（VRChat）](https://creators.vrchat.com/worlds/udon/players/player-collisions/)、[Event Nodes（VRChat）](https://creators.vrchat.com/worlds/udon/graph/event-nodes/)、[Collision module（Unity）](https://docs.unity3d.com/2022.3/Documentation/Manual/PartSysCollisionModule.html)
