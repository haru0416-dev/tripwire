このオブジェクトの CharacterController が、Move で動いている途中でコライダーに当たったときに起きます。Unity の説明では、当たった物を押すのに使える、とされています。

CharacterController は、Move を呼ばれて動く部品です。スクリプトや「Udon の機能を呼ぶ」で Move を呼ばないと、このイベントは起きません。プレイヤーに当たったことは、「[キャラクターコントローラーがプレイヤーにぶつかったとき](/reference/events/oncontrollercolliderhitplayer)」（OnControllerColliderHitPlayer）で受けます。

「ぶつかった情報」は ControllerColliderHit という情報のまとまりで、当たった相手のことが入っています。

公式の説明: [OnControllerColliderHit（Unity）](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.OnControllerColliderHit.html)、[Detect Controller Collide（VRChat）](https://creators.vrchat.com/worlds/examples/detect-controller-collide)
