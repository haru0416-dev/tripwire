Happens at the end of the frame when anyone's PlayerData has changed or been received. **Changed entries** lists all of that player's keys, each with its name (Key) and its state (State).

State is Unchanged, Added or Changed since the last update. Values brought back from saved records are Restored, which only happens when you join an instance you've been in before. Removed also exists, but keys can't be removed yet, so it doesn't appear.

You can only write your own PlayerData; other players' data is read-only. Changing any of your values sends all of your PlayerData, unchanged keys included.

**Whose** on the card starts as **Me**. That is enough for personal values such as a volume setting. For something like a total of everyone's scores, set it to **Anyone**.

Official docs: [PlayerData (VRChat)](https://creators.vrchat.com/worlds/udon/persistence/player-data/)
