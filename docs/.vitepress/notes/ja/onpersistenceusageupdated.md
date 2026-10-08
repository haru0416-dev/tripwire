保存データの使用量の情報が更新されたときに起きます。自分の PlayerData と PlayerObject の使用量は、「Udon の機能を呼ぶ」の Networking.RequestStorageUsageUpdate で計算を頼めます。その結果を受け取るのがこのイベントです。

使用量は Networking.GetPlayerDataStorageUsage と GetPlayerObjectStorageUsage で読めます。上限（バイト）は GetPlayerDataStorageLimit と GetPlayerObjectStorageLimit です。使用量の情報は時間がたつと古くなります。ただ、計算を頼む操作は何度も続けて呼ばないでください。

公式の説明: [PlayerData（VRChat）](https://creators.vrchat.com/worlds/udon/persistence/player-data/)、[PlayerObject（VRChat）](https://creators.vrchat.com/worlds/udon/persistence/player-object/)
