プレイヤーの PlayerObject の使用量が、保存できる上限に近づいたときに起きます。どこまで近づくと起きるかは、公式の説明に書かれていません。

上限は、1 つのワールドにつきプレイヤーごとに PlayerObject が 100 KB です。VRChat は圧縮して保存するので、圧縮しやすいデータなら 300 KB を超える量を入れられることもあります。今の使用量は、「Udon の機能を呼ぶ」の Networking.GetPlayerObjectStorageUsage で読めます。

カードの「誰のとき」は最初「自分」です。上限を超えると保存できなくなります。このイベントは、保存する量を減らすよう案内を出すのに使えます（[上限を超えたとき](/reference/events/onplayerobjectstorageexceeded)）。

公式の説明: [PlayerObject（VRChat）](https://creators.vrchat.com/worlds/udon/persistence/player-object/)、[Persistence（VRChat）](https://creators.vrchat.com/worlds/udon/persistence/)
