For **Event name**, write the card's **Name** when the target is a trigger, or the name of a public method when it is an UdonSharp script. When the target is this trigger itself and it has no Custom event with that name, Tripwire warns.

**Run on** picks where the event runs. **Only my screen** runs it right away on the screen where the action runs. **Everyone's screen** runs it on the screen of everyone in the instance, the sender included. **Owner's screen** runs it only on the screen of the target object's owner.

**Everyone's screen** and **Owner's screen** go over the network, so players who join later never get them. Keep a state they should see in a synced variable ([The same state for everyone](/en/guide/sync)). Network events have a send limit; events over it wait in a queue on your side instead of being dropped.

Over the network, only public methods whose names don't start with `_` can be called. Sending a name that starts with `_` over the network makes Tripwire warn.

Official docs: [Network Events (VRChat)](https://creators.vrchat.com/worlds/udon/networking/events/), [Special Nodes (VRChat)](https://creators.vrchat.com/worlds/udon/graph/special-nodes)
