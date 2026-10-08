プレイヤーの PlayerData の使用量が、保存できる上限に近づいたときに起きます。どこまで近づくと起きるかは、公式の説明に書かれていません。

上限は、1 つのワールドにつきプレイヤーごとに PlayerData が 100 KB です。VRChat は圧縮して保存するので、圧縮しやすいデータなら 300 KB を超える量を入れられることもあります。今の使用量は、「Udon の機能を呼ぶ」の Networking.GetPlayerDataStorageUsage で読めます。

カードの「誰のとき」は最初「自分」です。上限を超えると保存できなくなります。このイベントは、保存する量を減らすよう案内を出すのに使えます（[上限を超えたとき](/reference/events/onplayerdatastorageexceeded)）。

公式の説明: [PlayerData（VRChat）](https://creators.vrchat.com/worlds/udon/persistence/player-data/)、[Persistence（VRChat）](https://creators.vrchat.com/worlds/udon/persistence/)
