Happens when synced values arrive from the owner and have been written into the variables. It doesn't tell which variable changed. Players who join later get the latest values through this event too.

If this trigger has no synced variable, it receives no sync and this event never happens. On Variable Changed cards of the same trigger run before this card. To react to each variable separately, On Variable Changed is the better fit ([The same state for everyone](/en/guide/sync)).

The official docs don't say whether this event happens when you change a value yourself. To also react to your own changes, use On Variable Changed.

Official docs: [Network Components (VRChat)](https://creators.vrchat.com/worlds/udon/networking/network-components/), [Late Joiners & Sync Issues (VRChat)](https://creators.vrchat.com/worlds/udon/networking/late-joiners/)
