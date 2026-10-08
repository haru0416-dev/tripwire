Moves the targets to the position and rotation of **Destination**. Both are matched in world space, so the target lands on the destination even under a different parent. Scale doesn't change.

An empty object makes an easy destination: move it in the Scene view to set where and which way.

It moves only on the screen where the action runs; other players still see the old position. To keep everyone in step, add VRC Object Sync to the object so its position and rotation are synced. To move the player, use **Teleport Local Player**.

Official docs: [Transform.SetPositionAndRotation (Unity)](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Transform.SetPositionAndRotation.html), [VRC Object Sync (VRChat)](https://creators.vrchat.com/worlds/components/vrc_objectsync/)
