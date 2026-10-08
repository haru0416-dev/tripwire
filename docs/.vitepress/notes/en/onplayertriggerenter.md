When anyone walks in, this event happens on every present player's screen. **Who enters** on the card picks whose entering runs the actions:

- Me: only when you walk in; use this for teleports and personal effects
- Others: only when someone else walks in
- Anyone: whoever walks in; **That player** tells who

It starts as **Me**. With **Anyone**, each player walking in runs the actions on every screen, which suits things like playing a sound.

The area is a collider on this object with Is Trigger on, and VRChat checks it against each player's capsule collider. [Teleport](/en/recipes/teleport#teleport-on-entering-an-area) has an example that moves whoever walks in.

VRChat's docs warn that the event can be skipped when a player teleports in or out, or moves very fast. To notice a ball or another object entering, use [On Trigger Enter (Collider)](/en/reference/events/ontriggerenter).

Official docs: [Player Collisions (VRChat)](https://creators.vrchat.com/worlds/udon/players/player-collisions/), [Event Nodes (VRChat)](https://creators.vrchat.com/worlds/udon/graph/event-nodes/)
