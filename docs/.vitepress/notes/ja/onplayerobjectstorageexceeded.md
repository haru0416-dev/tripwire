プレイヤーの PlayerObject の使用量が、保存できる上限を超えたときに起きます。上限は、1 つのワールドにつきプレイヤーごとに 100 KB です。

上限を超えることになるデータは保存できません。VRChat はログにエラーを出し、そのデータは保存されません。保存する量を減らしてください。

カードの「誰のとき」は最初「自分」です。上限に近づいた段階で知りたいときは、[上限に近いとき](/reference/events/onplayerobjectstoragewarning)のイベントを使います。

公式の説明: [PlayerObject（VRChat）](https://creators.vrchat.com/worlds/udon/persistence/player-object/)、[Persistence（VRChat）](https://creators.vrchat.com/worlds/udon/persistence/)
