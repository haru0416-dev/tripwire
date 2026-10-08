Happens when a player's avatar has finished loading: yours, and other players' too. **That player** tells whose avatar it is, and **Whose** on the card picks whose changes run the actions. It starts as **Me**.

If you read your own eye height inside this event, you get the avatar's own height, not the height you saved. For other players, their new eye height may not have arrived yet, so don't rely on what you read here.

Players with a saved height switch to it after the avatar loads, which fires [On Avatar Eye Height Changed](/en/reference/events/onavatareyeheightchanged). Use that event for anything that depends on height.

Official docs: [Avatar Events (VRChat)](https://creators.vrchat.com/worlds/udon/avatar-events/)
