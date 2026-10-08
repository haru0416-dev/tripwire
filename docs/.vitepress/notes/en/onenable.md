Happens when this object becomes active. It also happens for objects that are active when the world starts, before Start. Unlike Start, it happens again every time the object is turned off and on.

**Set Active** turning the object on causes it. The object turns on only on the screens where that action ran, so this event happens only there too. To turn it on for everyone, switch it with a synced variable and On Variable Changed ([The same state for everyone](/en/guide/sync)).

Keep the card's advanced setting at **Only my screen**; anything else runs the actions once per player each time. Use this event for objects spawned from a VRC Object Pool too.

Official docs: [MonoBehaviour.OnEnable (Unity)](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.OnEnable.html), [Order of execution for event functions (Unity)](https://docs.unity3d.com/2022.3/Documentation/Manual/ExecutionOrder.html)
