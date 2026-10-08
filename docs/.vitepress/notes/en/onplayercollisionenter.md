Happens when a player's capsule collider hits this object's collider (one with Is Trigger off). It fires for any player in the instance, so **Whose** on the card picks Me, Others or Anyone.

VRChat's docs say it is not called when a player walks into a stationary object. It is meant for moving things, like balls or bullets, that hit a player. To notice a player coming up to a wall or similar, use an area with [On Player Trigger Enter](/en/reference/events/onplayertriggerenter).

Colliders on the Walkthrough and Pickup layers don't collide with players, so objects on those layers never count as a hit.

Official docs: [Player Collisions (VRChat)](https://creators.vrchat.com/worlds/udon/players/player-collisions/), [Event Nodes (VRChat)](https://creators.vrchat.com/worlds/udon/graph/event-nodes/), [Layers (VRChat)](https://creators.vrchat.com/worlds/layers/)
