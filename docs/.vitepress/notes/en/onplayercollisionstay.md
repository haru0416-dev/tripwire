Happens again and again while a player's capsule touches this object's collider. VRChat's docs describe it as every frame.

Like On Player Collision Enter, it is meant for moving things touching a player, and isn't called when a player walks into a stationary object. If you only need the start and the end of the contact, [On Player Collision Enter](/en/reference/events/onplayercollisionenter) and On Player Collision Exit are lighter.

Official docs: [Player Collisions (VRChat)](https://creators.vrchat.com/worlds/udon/players/player-collisions/), [Event Nodes (VRChat)](https://creators.vrchat.com/worlds/udon/graph/event-nodes/)
