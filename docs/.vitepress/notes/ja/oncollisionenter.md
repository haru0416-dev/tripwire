このオブジェクトのコライダーが、ほかのコライダーと触れ始めたときに起きます。どちらも Is Trigger がオフのコライダーで、少なくとも片方に Is Kinematic がオフの Rigidbody が必要です。そのため、Is Kinematic をオンにした物と、Rigidbody のない床などの間では起きません。

「ぶつかった相手」は Collision という情報のまとまりで、当たった点や当たったときの速さなどが入っています。

床に置く側と落ちてくる側のどちらにトリガーを付けても使えます。たとえば Rigidbody の付いた箱が床に落ちると、床のトリガーでも箱のトリガーでも起きます。

プレイヤーが当たったことを知りたいときは「[プレイヤーがぶつかったとき](/reference/events/onplayercollisionenter)」を使います。

公式の説明: [OnCollisionEnter（Unity）](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.OnCollisionEnter.html)、[Interaction between collider types（Unity）](https://docs.unity3d.com/2022.3/Documentation/Manual/collider-types-interaction.html)
