Turns components with an enable checkbox on or off: Light, Animator, AudioSource, UdonBehaviour and so on. Use it to stop just a light or an animation without hiding the whole object.

A disabled component no longer gets its per-frame Update. It is the same as the checkbox next to the component's name in the Inspector.

It changes only on the screen where the action runs. To keep everyone in step, react to a synced variable with On Variable Changed and run this action there ([The same state for everyone](/en/guide/sync)).

Official docs: [Behaviour.enabled (Unity)](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Behaviour-enabled.html)
