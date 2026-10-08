範囲に入っているコライダーごとに、物理演算の更新のたびに起きます。範囲に 3 つ入っていれば、1 回の更新で 3 回起きます。「中にいる物」は、そのときの相手のコライダーです。

起きる条件は「物体が範囲に入ったとき」と同じで、どちらかに Rigidbody が必要です（[物体が範囲に入ったとき](/reference/events/ontriggerenter)）。入ったときと出たときだけ分かれば足りるなら、そちらと「物体が範囲から出たとき」を使うほうが軽く済みます。

公式の説明: [OnTriggerStay（Unity）](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.OnTriggerStay.html)
