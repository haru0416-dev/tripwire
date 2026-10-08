オーナーから同期する変数の値が届き、変数に入り終わったときに起きます。どの変数が変わったかは分かりません。あとから来た人にも、最新の値がこのイベントで届きます。

このトリガーに同期する変数がないと、同期を受け取らないので起きません。同じトリガーの「変数が変わったとき」は、このカードより先に動きます。変数ごとに動きを分けるなら、「変数が変わったとき」のほうが向いています（[全員で同じ状態にする](/guide/sync)）。

自分で値を変えたときにこのイベントが起きるかは、公式の説明に書かれていません。自分の操作にも反応させたいときは、「変数が変わったとき」を使ってください。

公式の説明: [Network Components（VRChat）](https://creators.vrchat.com/worlds/udon/networking/network-components/)、[Late Joiners & Sync Issues（VRChat）](https://creators.vrchat.com/worlds/udon/networking/late-joiners/)
