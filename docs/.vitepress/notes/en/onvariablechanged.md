Happens when the card's **Variable** changes. For a synced variable, that is when you change it, when someone else does, and when a player who joins later receives the current value. It is how every screen gets to the same state ([The same state for everyone](/en/guide/sync)).

Setting the same value again doesn't count as a change. Don't change the watched variable inside this event: each change runs it again. Temporary variables can't be watched.

Changing a synced variable makes you the object's owner, and your value is sent to everyone. On other screens the event happens when the value arrives and differs from the one they had.

On the screen of a player who joins later, it happens only if the received value differs from the variable's **Initial** value. If it still equals the initial value, nothing runs, so make the scene match the initial value: a light whose initial value is Off should start switched off in the scene.

VRChat sends late joiners the latest values of synced variables, but not past network events. To keep a state everyone shares, use this event with a synced variable.

Official docs: [Late Joiners (VRChat)](https://creators.vrchat.com/worlds/udon/networking/late-joiners), [Network Variables (VRChat)](https://creators.vrchat.com/worlds/udon/networking/variables)
