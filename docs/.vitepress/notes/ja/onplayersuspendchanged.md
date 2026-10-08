誰かの端末が中断したときに起きます。中断とは、端末がスリープしたり、別のアプリに切り替えたりすることです。中断した本人の画面では、戻ってきたときに起きます。カードの「誰のとき」で、どの人のときに動かすかを選びます。

中断か再開かは、そのプレイヤーの isSuspended で見分けます。「Udon の機能を呼ぶ（Udon API）」で isSuspended を変数に入れ、If で分けてください。自分の isSuspended はいつもオフです。そのため「誰のとき」が「自分」なら、このイベントは自分が戻ってきたときに起きます。

中断している端末では Udon が動かず、ネットワークのイベントも受け取りません。公式の説明では、大事なオブジェクトのオーナーを中断していない人に移すよう勧めています（「オーナーになる」）。今のところ PC は中断しませんが、どの端末でも起こりうるものとして作るよう案内されています。

公式の説明: [Event Nodes（VRChat）](https://creators.vrchat.com/worlds/udon/graph/event-nodes/)、[Player API（VRChat）](https://creators.vrchat.com/worlds/udon/players/)
