Happens when the saved-data usage information has been updated. In **Call Udon API**, Networking.RequestStorageUsageUpdate asks for your own PlayerData and PlayerObject usage to be calculated; the result arrives through this event.

Networking.GetPlayerDataStorageUsage and GetPlayerObjectStorageUsage read the usage, and GetPlayerDataStorageLimit and GetPlayerObjectStorageLimit read the limits in bytes. Usage information can go stale over time, but avoid calling RequestStorageUsageUpdate often.

Official docs: [PlayerData (VRChat)](https://creators.vrchat.com/worlds/udon/persistence/player-data/), [PlayerObject (VRChat)](https://creators.vrchat.com/worlds/udon/persistence/player-object/)
