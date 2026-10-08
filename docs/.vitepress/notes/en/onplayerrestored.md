Happens when all of a player's saved data (both PlayerData and PlayerObjects) has finished loading. It happens even for a player with no saved data. When you join, it runs for every player in the instance, you included. When someone else joins, it runs only for them.

Read or write saved data only after this event. At On Player Joined, the data may not have arrived yet, and data written too early can be overwritten when it arrives.

**Whose** on the card starts as **Me**, so the actions run only when your own data is loaded. To read and write PlayerData, search for PlayerData in **Call Udon API**.

With Build & Test, saved data is kept locally for each test client. A client starts with no data, and its data is deleted when you close it.

Official docs: [Event Nodes (VRChat)](https://creators.vrchat.com/worlds/udon/graph/event-nodes/), [PlayerData (VRChat)](https://creators.vrchat.com/worlds/udon/persistence/player-data/), [Persistence (VRChat)](https://creators.vrchat.com/worlds/udon/persistence/)
