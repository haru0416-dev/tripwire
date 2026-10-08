When a player leaves the instance, this happens on the screen of each player who remains. **Who leaves the instance** starts as **Me**. VRChat's docs don't say whether it happens when you yourself leave, so to react to others leaving, pick **Others** or **Anyone**.

**That player** is someone no longer in the instance. Use it right away inside the event (to show the name, say). Kept in a variable for later, the player may no longer be valid.

If the leaving player owned an object, VRChat picks a new owner automatically. [On Ownership Transferred](/en/reference/events/onownershiptransferred) tells you when that happens.

Keep the advanced settings on **Only my screen**. Otherwise every screen sends its own copy of the event, and the actions run once per player.

Official docs: [Event Nodes (VRChat)](https://creators.vrchat.com/worlds/udon/graph/event-nodes/), [Object Ownership (VRChat)](https://creators.vrchat.com/worlds/udon/networking/ownership)
