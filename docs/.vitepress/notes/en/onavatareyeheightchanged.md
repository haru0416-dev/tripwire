Happens when a player's eye height changes, either by switching avatar or by avatar scaling. Other players' changes count too. **Whose** on the card picks whose changes run the actions.

**Previous eye height** is the height before the change, in meters. The first one after joining may be 0. To read the current height, use **Call Udon API** with GetAvatarEyeHeightAsMeters; it works for other players too.

When you change avatar and have a saved height, it fires only for the saved height. For other players it fires each time a new height reaches you, so one avatar change can fire it more than once. It can even come before [On Avatar Changed](/en/reference/events/onavatarchanged), but heights never arrive out of order.

Players can scale themselves to eye heights from 0.2 to 5 m. Scaling doesn't change how big they are for collisions with the world.

Official docs: [Avatar Events (VRChat)](https://creators.vrchat.com/worlds/udon/avatar-events/), [Player Avatar Scaling (VRChat)](https://creators.vrchat.com/worlds/udon/players/player-avatar-scaling/)
