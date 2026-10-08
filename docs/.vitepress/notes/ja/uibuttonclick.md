カードの「対象の UI」に入れたボタンが押されたときに起きます。Tripwire は変換のとき（Play とビルドの前、または「今すぐ反映」）に、ボタンの On Click へトリガーを呼ぶ設定を足します。Tripwire が触るのは自分で足した設定だけで、On Click にあるほかの設定はそのままです。

Unity のボタンは、押して離したときに On Click が動きます。押したままポインターをボタンの外へ出して離すと、動きません。

Canvas に VRC Ui Shape がないと、カードに警告と「VRC Ui Shape を付ける」ボタンが出ます。Canvas のレイヤーが UI のままだと、VRChat のメニューを開いている間しか押せません。Default などのレイヤーに変えてください。

ボタンを押したときのアクションは、押した人の画面で動きます。全員に結果を見せるときは、同期する変数を変えてから「変数が変わったとき」で受けてください（[全員で同じ状態にする](/guide/sync)）。

公式の説明: [VRC Ui Shape（VRChat）](https://creators.vrchat.com/worlds/components/vrc_uishape/)、[UI Events（VRChat）](https://creators.vrchat.com/worlds/udon/ui-events/)、[Button（Unity UI）](https://docs.unity3d.com/Packages/com.unity.ugui@1.0/manual/script-Button.html)
