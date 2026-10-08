ほかのコライダーが、このオブジェクトの範囲（Is Trigger をオンにしたコライダー）に入ったときに起きます。物理演算の更新（FixedUpdate）のときに呼ばれる、Unity の物理演算のイベントです。「入ってきた物」は、入ってきた相手のコライダーです。

起きるには、両方にコライダーがあり、少なくとも片方は Is Trigger がオンであることが条件です。さらに、どちらかに Rigidbody が付いている必要があります。Unity の組み合わせ表では、Rigidbody の付いた範囲は、相手がどんなコライダーでも起きます。Rigidbody のない範囲が反応するのは、Rigidbody の付いた物（Is Kinematic がオンでも可）が入ったときだけです。

Unity はこのイベントを、範囲の側と入ってきた側の両方に送るので、範囲を持たない側のトリガーでも起きます。たとえばボールに付けたトリガーでも、ゴールの範囲に入ったことが分かります。

プレイヤーが入ったことを知りたいときは「[プレイヤーが範囲に入ったとき](/reference/events/onplayertriggerenter)」を使います。

公式の説明: [OnTriggerEnter（Unity）](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.OnTriggerEnter.html)、[OnTriggerExit（Unity）](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.OnTriggerExit.html)、[Interaction between collider types（Unity）](https://docs.unity3d.com/2022.3/Documentation/Manual/collider-types-interaction.html)
