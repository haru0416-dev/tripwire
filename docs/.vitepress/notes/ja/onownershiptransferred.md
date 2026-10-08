このオブジェクトのオーナーが変わると、インスタンスにいる全員の画面で起きます。「そのプレイヤー」は新しいオーナーです。カードの「誰のとき」は最初「自分」なので、そのままだと自分がオーナーになったときだけ動きます。

Tripwire では、同期する変数を変えた人が自動でオーナーになります。前のオーナーと別の人がスイッチを押せば、このイベントも起きます。「オーナーになる」アクションを使ったときも同じです。

最初のオーナーは、インスタンスに最初に入った人です。オーナーが退出すると、VRChat が自動で新しいオーナーを決めます。

公式の説明: [Network Components（VRChat）](https://creators.vrchat.com/worlds/udon/networking/network-components/)、[Object Ownership（VRChat）](https://creators.vrchat.com/worlds/udon/networking/ownership/)
