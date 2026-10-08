Like On Player Trigger Enter, it happens on every screen, with **Who leaves the area** picking Me, Others or Anyone. Often used to undo what entering the area turned on.

The area is a collider with Is Trigger on, checked against each player's capsule collider. VRChat's docs warn that the event can be skipped when a player teleports out or moves very fast. If something teleports players out of the area, the undo here may not run.

Official docs: [Player Collisions (VRChat)](https://creators.vrchat.com/worlds/udon/players/player-collisions/), [Event Nodes (VRChat)](https://creators.vrchat.com/worlds/udon/graph/event-nodes/)
