カードの「表示する文字」に書いた文字が、オブジェクトを指したときに出ます。何も書かなければ、VRChat の標準の「Use」が出ます。

クリックしたときのアクションは、ふつうはクリックした人の画面でだけ動きます。押した結果を全員に見せたいときは、同期する変数を変えてから「変数が変わったとき」で受ける形にしてください（[全員で共有するスイッチ](/recipes/shared-switch)）。

詳細設定を「全員（All）」にしても、アクションが動くのはその時点でいる人の画面だけです。VRChat のネットワークのイベントは、あとから来た人には送り直されません。

クリックできるのは、コライダーの付いたオブジェクトです。指す光線は VRChat のほとんどのレイヤーで遮られるので、手前に別のコライダーがあると押せないことがあります。光線を通すのは UiMenu・UI・PlayerLocal・MirrorReflection のレイヤーです。ユーザーレイヤーも、Interact Passthrough に入れない限り光線を遮ります。

Canvas の UI のボタンなら、このイベントより「[ボタンが押されたとき](/reference/events/uibuttonclick)」が向いています。

公式の説明: [Event Nodes（VRChat）](https://creators.vrchat.com/worlds/udon/graph/event-nodes/)、[Layers（VRChat）](https://creators.vrchat.com/worlds/layers/)、[Custom Network Events（VRChat）](https://creators.vrchat.com/worlds/udon/networking/events)
