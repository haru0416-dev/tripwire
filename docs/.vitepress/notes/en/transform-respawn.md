Puts the objects in **Targets** back where they were, position and rotation, when the world started: a reset button for scattered pickups. The place is taken when this trigger starts (usually when the world loads), so drag the objects into **Targets**.

An object with a VRC Object Sync goes back through Object Sync's Respawn after taking ownership, so it returns on everyone's screen. Others go back only on the screen where the action runs, with their Rigidbody's movement and spin stopped.

A pickup you are holding is dropped first; one someone else holds stays in their hand.

Keep the card on **Only my screen** (Local). On everyone's screen, every player would take ownership of the Object Sync objects at once to put them back.

Official docs: [VRC Object Sync (VRChat)](https://creators.vrchat.com/worlds/components/vrc_objectsync/), [VRC Pickup (VRChat)](https://creators.vrchat.com/worlds/components/vrc_pickup/)
