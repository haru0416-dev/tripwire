物理演算の間隔ごとに起きます。Unity のデフォルトの間隔は 0.02 秒（1 秒に 50 回）です。物理演算は、このイベントの後に計算されます。

画面のフレームとは別に数えるので、1 フレームに何度も起きることも、1 度も起きないこともあります。たとえば 25 fps なら 1 フレームにおよそ 2 回、100 fps なら 2 フレームにおよそ 1 回です。

公式の説明: [MonoBehaviour.FixedUpdate（Unity）](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.FixedUpdate.html)
