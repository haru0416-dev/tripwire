Happens again and again while a player's capsule is inside the area (a collider with Is Trigger on). VRChat's docs describe it as every frame, for any player in the instance. **Whose** on the card starts as **Me**.

If entering and leaving are enough, use [On Player Trigger Enter](/en/reference/events/onplayertriggerenter) and On Player Trigger Exit instead.

Starting a timer here restarts it each time, so it never fires. Changing a synced variable here sends it each time it changes, which can delay other sync.

Official docs: [Player Collisions (VRChat)](https://creators.vrchat.com/worlds/udon/players/player-collisions/), [Event Nodes (VRChat)](https://creators.vrchat.com/worlds/udon/graph/event-nodes/)
