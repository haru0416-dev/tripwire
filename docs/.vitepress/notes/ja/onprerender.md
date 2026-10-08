Camera と同じオブジェクトに付けたトリガーでだけ起きます。カリングが終わった後、そのカメラが描画を始める直前です。

カリングの後なので、映るものに関わる変更をここで入れても、反映されるのは次のフレームからです。映るものを変えたいときは「カメラが描画を始める前（カリング前）」を使います（[OnPreCull](/reference/events/onprecull)）。プレイヤーの視点のカメラは対象外で、ワールドに置いたカメラに使います。

公式の説明: [MonoBehaviour.OnPreRender（Unity）](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.OnPreRender.html)
