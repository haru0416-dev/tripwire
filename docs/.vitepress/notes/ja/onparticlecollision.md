パーティクルの粒がコライダーに当たったときに起きます。このイベントは、パーティクルシステムの側にも、当たられたコライダーの側にも置けます。パーティクルシステムに置いたときの「相手のオブジェクト」は、粒が当たったコライダーのオブジェクトです。コライダーの側に置いたときは、粒を出したパーティクルシステムのオブジェクトです。

起きるには、パーティクルシステムの Collision モジュールで Send Collision Messages をオンにします。粒が当たるのは、Collision モジュールの Collides With で選んだレイヤーの物だけです。

同じパーティクルシステムの粒がいくつ当たっても、1 つのコライダーに届くのは 1 フレームに 1 回までです。当たった位置など粒ごとの情報は、このイベントの値には入っていません。プレイヤーに当たったことは「[パーティクルがプレイヤーに当たったとき](/reference/events/onplayerparticlecollision)」で受けます。

公式の説明: [OnParticleCollision（Unity）](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.OnParticleCollision.html)、[Collision module（Unity）](https://docs.unity3d.com/2022.3/Documentation/Manual/PartSysCollisionModule.html)
