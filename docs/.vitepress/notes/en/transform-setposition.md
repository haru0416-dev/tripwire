**Position** is in world space: x, y, z within the whole scene. It can be a fixed value, a position variable, or a value the event brings. With several targets, all of them move to the same spot. Rotation stays as it is.

It moves only on the screen where the action runs. Other players still see the old position, and players who join later don't get it either. To keep everyone in step, add VRC Object Sync to the object: it syncs the position and rotation for everyone.

For fixed spots, put an empty object there as a marker and use **Move To**; the spot is easier to check in the Scene view.

Official docs: [Transform.position (Unity)](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Transform-position.html), [VRC Object Sync (VRChat)](https://creators.vrchat.com/worlds/components/vrc_objectsync/)
