When a player joins the instance, this happens on every present player's screen. When you join, your screen gets it once for each player already there, and once for you.

**Who joins** starts as **Me**: the actions run once, when you join. With **Anyone**, they run once per player in the instance at the moment you join. With **Others**, they run for each player already there when you join, then each time someone else arrives.

Keep the advanced settings on **Only my screen**. Otherwise every screen sends its own copy of the event, and the actions run once per player.

To show the current state to players who join later, a synced variable is more reliable than this event: VRChat sends late joiners the latest values of synced variables ([The same state for everyone](/en/guide/sync)).

Official docs: [Event Nodes (VRChat)](https://creators.vrchat.com/worlds/udon/graph/event-nodes/), [Late Joiners (VRChat)](https://creators.vrchat.com/worlds/udon/networking/late-joiners)
