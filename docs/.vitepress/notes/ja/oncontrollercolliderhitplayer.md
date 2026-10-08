このオブジェクトの CharacterController が、Move で動いている途中でプレイヤーに当たったときに起きます。VRChat が Unity の OnControllerColliderHit に足したイベントです。プレイヤー以外の物に当たったことは、「[キャラクターコントローラーがぶつかったとき](/reference/events/oncontrollercolliderhit)」で受けます。

ぶつかる側はこのオブジェクトの CharacterController で、当たられる側がプレイヤーです。プレイヤーの体が動いて物に当たったことを知るイベントではありません。それを知りたいときは、範囲と「プレイヤーが範囲に入ったとき」を使います。

「ぶつかった情報」には、当たったプレイヤーが入っています。VRChat の例では、CharacterController を Update のたびに Move で前へ進め、当たったプレイヤーの名前をテキストに出しています。Move を呼ぶのは、スクリプトや「Udon の機能を呼ぶ」の役目です。

公式の説明: [Detect Controller Collide（VRChat）](https://creators.vrchat.com/worlds/examples/detect-controller-collide)、[Player Collisions（VRChat）](https://creators.vrchat.com/worlds/udon/players/player-collisions/)
