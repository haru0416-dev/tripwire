Happens when a player's PlayerObject usage is getting close to the storage limit. The official docs don't say how close it has to be.

The limit is 100 KB of PlayerObject per player for each world. VRChat stores the data compressed, so data that compresses well can go over 300 KB. **Call Udon API** reads the current usage with Networking.GetPlayerObjectStorageUsage.

**Whose** on the card starts as **Me**. Going over the limit stops saving, so this is a place to warn the player or save less ([when over the limit](/en/reference/events/onplayerobjectstorageexceeded)).

Official docs: [PlayerObject (VRChat)](https://creators.vrchat.com/worlds/udon/persistence/player-object/), [Persistence (VRChat)](https://creators.vrchat.com/worlds/udon/persistence/)
