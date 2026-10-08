このオブジェクトのレンダラーが、どれかのカメラに映るようになったときに起きます。知らせはレンダラーに付いたスクリプトに届くので、トリガーはレンダラーと同じオブジェクトに付けます。

見えているときだけ要る処理を、見えないあいだ止めておくのに向いています。対になるのは「画面から消えたとき」です（[OnBecameInvisible](/reference/events/onbecameinvisible)）。エディターで動かしているときは、Scene ビューのカメラに映っても起きます。

公式の説明: [MonoBehaviour.OnBecameVisible（Unity）](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.OnBecameVisible.html)
