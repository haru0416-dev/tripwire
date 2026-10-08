Happens when this object's CharacterController hits a player while moving with Move. VRChat adds it next to Unity's OnControllerColliderHit; for hits on anything other than a player, use [On Controller Collider Hit](/en/reference/events/oncontrollercolliderhit).

The CharacterController on this object does the hitting, and the player is what gets hit. It is not a way to notice a player's own body running into something; for that, use an area with On Player Trigger Enter.

**The hit** holds the player who was hit. VRChat's example moves the CharacterController forward with Move on every Update and shows the hit player's name in a text. Calling Move is up to a script or **Call Udon API**.

Official docs: [Detect Controller Collide (VRChat)](https://creators.vrchat.com/worlds/examples/detect-controller-collide), [Player Collisions (VRChat)](https://creators.vrchat.com/worlds/udon/players/player-collisions/)
