Happens when particles hit a collider. The event works on either side, the particle system or the collider that was hit, and **The other object** depends on which:

- On the particle system: the object of the collider that was hit
- On the collider's object: the object of the particle system that hit it

The particle system's Collision module needs Send Collision Messages on. Particles only collide with objects on the layers chosen in the module's Collides With.

However many particles from one system hit a collider, it gets at most one call per frame. Per-particle details such as the hit position are not part of the event's value. Hits on players arrive in [On Player Particle Collision](/en/reference/events/onplayerparticlecollision).

Official docs: [OnParticleCollision (Unity)](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.OnParticleCollision.html), [Collision module (Unity)](https://docs.unity3d.com/2022.3/Documentation/Manual/PartSysCollisionModule.html)
