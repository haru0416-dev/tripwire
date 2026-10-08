2D 物理のイベントです。Box Collider 2D などの 2D 用のコライダーが、このオブジェクトの 2D の範囲（Is Trigger をオンにした Collider 2D）に入ったときに起きます。ふつうの 3D のコライダーやプレイヤーの出入りには使いません。

起きるには、どちらかのコライダーに Rigidbody 2D が付いている必要があります。範囲の側と入ってきた側の両方に、このイベントが届きます。3D の範囲のくわしい条件は「[物体が範囲に入ったとき](/reference/events/ontriggerenter)」を見てください。

公式の説明: [OnTriggerEnter2D（Unity）](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.OnTriggerEnter2D.html)
