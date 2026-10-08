Animator が内部の IK を更新する直前に起きます。IK の目標の位置や、その重みを決めるためのイベントです。「レイヤー番号」は、IK を計算する Animator のレイヤーの番号です。

同じフレームの「Animator がルートモーションを計算したとき」は、このイベントより前に起きます（[OnAnimatorMove](/reference/events/onanimatormove)）。

公式の説明: [MonoBehaviour.OnAnimatorIK（Unity）](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.OnAnimatorIK.html)
