範囲（Is Trigger をオンにしたコライダー）に入っていたコライダーが、出ていったときに起きます。「出ていった物」は、出ていった相手のコライダーです。範囲に入ったときに変えたものを、ここで元に戻すのによく使います。

起きる条件は「物体が範囲に入ったとき」と同じで、どちらかに Rigidbody が付いている必要があります。Unity はこのイベントを、範囲の側と出ていった側の両方に送ります（[物体が範囲に入ったとき](/reference/events/ontriggerenter)）。

公式の説明: [OnTriggerExit（Unity）](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.OnTriggerExit.html)、[Interaction between collider types（Unity）](https://docs.unity3d.com/2022.3/Documentation/Manual/collider-types-interaction.html)
