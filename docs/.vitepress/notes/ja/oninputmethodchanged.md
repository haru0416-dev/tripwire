自分の入力方法が変わったときに起きます。キーボードからマウスへ、ゲームパッドからタッチ画面へ、といった切り替えです。

「入力方法」は VRCInputMethod の値です。Keyboard や Mouse、Controller、Touch のほか、Vive や Oculus、Index、Pico のように機器ごとの値もあります。Vive は SteamVR で動く Vive のコントローラーです。ViveXR は OpenXR で動く Vive XR Elite のコントローラーです。

操作の説明を入力方法ごとに出し分けるときに使えます。「くわしく」で VRCInputMethod の変数を作って「変数に値を入れる」で値を入れ、If で比べてください。

公式の説明: [Input Events（VRChat）](https://creators.vrchat.com/worlds/udon/input-events/)、[Type Nodes（VRChat）](https://creators.vrchat.com/worlds/udon/graph/type-nodes/)
