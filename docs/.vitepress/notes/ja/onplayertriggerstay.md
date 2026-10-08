プレイヤーのカプセルが範囲（Is Trigger をオンにしたコライダー）の中にいる間、続けて起きます。VRChat の説明では、インスタンスにいる誰についても「毎フレーム」起きるとされています。カードの「誰のとき」は最初「自分」です。

入ったときと出たときだけで足りる仕掛けなら、「[プレイヤーが範囲に入ったとき](/reference/events/onplayertriggerenter)」と「プレイヤーが範囲から出たとき」を使ってください。

このイベントで「タイマーを始める」を動かすと、そのたびに始め直すので、タイマーが鳴りません。同期する変数を変えると、変わるたびに送られて、ほかの同期が遅れることがあります。

公式の説明: [Player Collisions（VRChat）](https://creators.vrchat.com/worlds/udon/players/player-collisions/)、[Event Nodes（VRChat）](https://creators.vrchat.com/worlds/udon/graph/event-nodes/)
