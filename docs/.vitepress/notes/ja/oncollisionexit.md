触れていたほかのコライダーが、このオブジェクトのコライダーから離れたときに起きます。「離れた相手」は Collision という情報のまとまりです。

起きる条件は「物体がぶつかったとき」と同じです。どちらも Is Trigger がオフのコライダーで、片方に Is Kinematic がオフの Rigidbody が必要です（[物体がぶつかったとき](/reference/events/oncollisionenter)）。

公式の説明: [OnCollisionExit（Unity）](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.OnCollisionExit.html)
