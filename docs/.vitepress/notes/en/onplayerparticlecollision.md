Happens when a particle from this particle system hits a player's capsule. It fires for any player, so **Whose** on the card picks Me, Others or Anyone; for an effect only when you are hit, pick **Me**.

The particle system needs its Collision module on, with Send Collision Messages checked. VRChat's example sets the Collision type to World. Particles only collide with objects on the layers chosen in the module's Collides With.

Hits on other objects arrive in [On Particle Collision](/en/reference/events/onparticlecollision).

Official docs: [Player Collisions (VRChat)](https://creators.vrchat.com/worlds/udon/players/player-collisions/), [Event Nodes (VRChat)](https://creators.vrchat.com/worlds/udon/graph/event-nodes/), [Collision module (Unity)](https://docs.unity3d.com/2022.3/Documentation/Manual/PartSysCollisionModule.html)
