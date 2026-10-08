Happens only on a trigger attached to the same object as a Camera, after culling, just before that camera renders.

Because culling is done, a change that affects what the camera sees takes effect from the next frame. To change what it sees, use On Pre Cull ([On Pre Cull](/en/reference/events/onprecull)). It works with cameras placed in the world, not the player's view camera.

Official docs: [MonoBehaviour.OnPreRender (Unity)](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.OnPreRender.html)
