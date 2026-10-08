Happens each time a camera has finished rendering the scene. Unlike On Post Render, it happens on triggers that aren't on a camera too.

Unity meant it for drawing things yourself, and says not to change high-level rendering states in it. Every object with this event adds to the cost.

Official docs: [MonoBehaviour.OnRenderObject (Unity)](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.OnRenderObject.html)
