プレイヤーが VRChat のグラフィック設定で、視野角（Field of View）や Near Clip Override などを変えたときに起きます。自分の画面だけで起きます。「カメラ設定」は、画面のカメラ（ScreenCamera）か手持ちのカメラ（PhotoCamera）のどちらかです。

スクリプトから VRCCameraSettings の値を変えても、このイベントは起きません。カメラの位置や向きが変わっても起きません。

ただし、何度も起きる場面があります。手持ちのカメラのズームを動かしているあいだと、ウィンドウの大きさを変えているあいだは、毎フレーム起きます。1 フレームに何度も起きることもあるので、アクションは軽くしてください。

公式の説明: [Event Nodes（VRChat）](https://creators.vrchat.com/worlds/udon/graph/event-nodes/)、[VRCCameraSettings（VRChat）](https://creators.vrchat.com/worlds/udon/vrc-graphics/vrc-camera-settings/)
