Happens when you pick up the object with the VRC Pickup, on your screen only. To show the result to others, change a synced variable and react with On Variable Changed ([The same state for everyone](/en/guide/sync)).

A pickup needs a Rigidbody and a collider on the same object. The card's **Add VRC Pickup** also adds a collider if there is none, and the Rigidbody comes with the VRC Pickup.

By default, other players don't see the object move while you carry it. Add a VRC Object Sync to the same object to sync its movement. The text shown when pointing at it is the VRC Pickup's Interaction Text.

[Locked door](/en/recipes/locked-door) has a key that opens a door when picked up.

Official docs: [VRC Pickup (VRChat)](https://creators.vrchat.com/worlds/components/vrc_pickup/), [Event Nodes (VRChat)](https://creators.vrchat.com/worlds/udon/graph/event-nodes/)
