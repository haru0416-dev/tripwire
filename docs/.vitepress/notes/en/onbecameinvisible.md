Happens when this object's renderer is no longer visible to any camera. Put the trigger on the same object as the renderer.

Use it to pause work that isn't needed while unseen, and resume it with On Became Visible ([On Became Visible](/en/reference/events/onbecamevisible)). In the editor, Scene view cameras count too, so it won't happen while the Scene view shows the object.

Official docs: [MonoBehaviour.OnBecameInvisible (Unity)](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.OnBecameInvisible.html)
