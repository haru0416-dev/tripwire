このオブジェクトが見えているとき、描画するカメラごとに 1 回ずつ起きます。カリングの途中、そのオブジェクトを描く直前です。UI の要素では起きず、トリガーが無効のあいだも起きません。

カメラが複数あれば 1 フレームに何度も起きます。カメラの数によって回数が変わるので、回数を数える使い方には向きません。

公式の説明: [MonoBehaviour.OnWillRenderObject（Unity）](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.OnWillRenderObject.html)
