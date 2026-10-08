Happens at every physics step. Unity's default is every 0.02 seconds (50 times a second). The physics calculation runs after this event.

It is counted apart from the screen's frames, so it can happen several times in one frame or not at all. At 25 fps it runs about twice per frame; at 100 fps, about once every two frames.

Official docs: [MonoBehaviour.FixedUpdate (Unity)](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.FixedUpdate.html)
