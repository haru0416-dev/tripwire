プレイヤーがインスタンスから出ると、残っている人の画面でそれぞれ起きます。カードの「誰が退出したら」は最初「自分」です。公式の説明には、自分が退出するときに起きるかは書かれていません。ほかの人の退出で動かすなら「自分以外」か「誰でも」にしてください。

「そのプレイヤー」は、もうインスタンスにいない人です。名前を出すなど、イベントの中ですぐ使うだけにしてください。変数に入れておいて後で使うと、そのプレイヤーは無効になっていることがあります。

退出した人がオブジェクトのオーナーだったときは、VRChat が自動で新しいオーナーを決めます。オーナーが移ったことは「[オーナーが変わったとき](/reference/events/onownershiptransferred)」で分かります。

詳細設定は「自分だけ（Local）」のままにしてください。ほかの設定では、全員の画面で起きたイベントがそれぞれ送られるので、人数分くり返し動きます。

公式の説明: [Event Nodes（VRChat）](https://creators.vrchat.com/worlds/udon/graph/event-nodes/)、[Object Ownership（VRChat）](https://creators.vrchat.com/worlds/udon/networking/ownership)
