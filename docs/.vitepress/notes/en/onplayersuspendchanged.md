Happens when any player's device is suspended: it goes to sleep, or they switch to another app. On the suspended player's own screen it happens when they come back. **Whose** on the card picks whose devices run the actions.

To tell suspend from resume, read that player's isSuspended with **Call Udon API** into a variable and branch with If. Your own isSuspended is always off, so with **Whose** set to **Me** the event means you just came back.

A suspended device runs no Udon and receives no network events. The official docs suggest moving ownership of important objects to a player who isn't suspended (**Take Ownership**). PC players are never suspended today, but VRChat asks you not to assume that.

Official docs: [Event Nodes (VRChat)](https://creators.vrchat.com/worlds/udon/graph/event-nodes/), [Player API (VRChat)](https://creators.vrchat.com/worlds/udon/players/)
