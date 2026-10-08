Camera と同じオブジェクトに付けたトリガーでだけ起きます。そのカメラが、何が見えるかを決める処理（カリング）を始める直前です。ここでカメラの設定を変えると、そのカメラに映るものを変えられます。

プレイヤーの視点のカメラは、ワールドからは触れません。このイベントを使うのは、ワールドに自分で置いたカメラ（Render Texture に映すカメラなど）です。

公式の説明: [MonoBehaviour.OnPreCull（Unity）](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.OnPreCull.html)、[VRCCameraSettings（VRChat）](https://creators.vrchat.com/worlds/udon/vrc-graphics/vrc-camera-settings/)
