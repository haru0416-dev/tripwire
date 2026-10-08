Happens once for each camera that renders this object while it is visible: during culling, just before the object is drawn. It doesn't happen for UI elements, or while the trigger is disabled.

With several cameras it happens several times a frame, so don't use it to count anything.

Official docs: [MonoBehaviour.OnWillRenderObject (Unity)](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.OnWillRenderObject.html)
