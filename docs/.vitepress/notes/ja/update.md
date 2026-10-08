このトリガーが有効なあいだ、毎フレーム起きます。同じフレームの「毎フレーム（Update の後）」は、すべての Update が終わってから動きます。

一定の間隔で何かをしたいときは、毎フレームより「タイマー（一定時間ごと）」のほうが向いています（[タイマー](/reference/events/timer)）。このイベントは全員の画面でそれぞれ起きるので、ここで同期する変数を変えるのは避けてください。

公式の説明: [MonoBehaviour.Update（Unity）](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.Update.html)、[LateUpdate（Unity）](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.LateUpdate.html)
