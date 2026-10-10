Flips components such as lights, audio sources, Animators or other UdonBehaviours: enabled ones become disabled and the other way round. The object itself stays shown. To set a definite state, use [Set Component Enabled](/en/reference/actions/behaviour-setenabled).

It flips them only on the screen where the action runs. To keep everyone the same, toggle a synced variable and, in On Variable Changed, put the variable into **Set Component Enabled** ([The same state for everyone](/en/guide/sync)).

Official docs: [Behaviour.enabled (Unity)](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Behaviour-enabled.html)
