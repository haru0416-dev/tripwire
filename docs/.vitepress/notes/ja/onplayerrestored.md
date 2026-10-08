プレイヤーの保存データ（PlayerData と PlayerObject の両方）が読み込み終わったときに起きます。保存データがまだない人も対象です。自分が入ったときは、自分を含むその場の全員の分が起きます。ほかの人が入ってきたときは、その人の分だけ起きます。

保存データを読んだり書いたりするのは、このイベントの後にしてください。「プレイヤーが参加したとき」の時点では、まだデータが届いていないことがあります。早く書きすぎると、あとから届いたデータで上書きされます。

カードの「誰のとき」は最初「自分」なので、自分のデータが読み込まれたときだけ動きます。PlayerData の読み書きは、「Udon の機能を呼ぶ」で PlayerData を検索して選びます。

Build & Test では、保存データはテスト用のクライアントごとに手元に保存されます。クライアントは空の状態で始まり、閉じるとそのデータは消えます。

公式の説明: [Event Nodes（VRChat）](https://creators.vrchat.com/worlds/udon/graph/event-nodes/)、[PlayerData（VRChat）](https://creators.vrchat.com/worlds/udon/persistence/player-data/)、[Persistence（VRChat）](https://creators.vrchat.com/worlds/udon/persistence/)
