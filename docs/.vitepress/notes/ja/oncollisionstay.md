ほかのコライダーと触れている間、触れている相手ごとに続けて起きます。「触れている相手」は Collision という情報のまとまりです。

Rigidbody は、Sleep Threshold より遅く動くと sleep 状態になります。sleep 状態の間は触れていても起きないので、床で止まった箱などでは途中で起きなくなることがあります。

起きる条件は「物体がぶつかったとき」と同じです（[物体がぶつかったとき](/reference/events/oncollisionenter)）。

公式の説明: [OnCollisionStay（Unity）](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.OnCollisionStay.html)、[Rigidbody の sleep（Unity）](https://docs.unity3d.com/2022.3/Documentation/Manual/RigidbodiesOverview.html)
