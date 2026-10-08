Fires a Trigger parameter of the Animator Controller, by name. Use the same name as in the Parameters list of the Animator window.

A Trigger is a parameter the Animator turns back off once a transition uses it. It suits one-off motions: open a door once, play a jump once.

Only the Animator on the screen where the action runs gets it. Being a one-off signal, it never reaches players who join later. For a state everyone should share, such as open or closed, use a synced variable with **Animator Set Bool** ([The same state for everyone](/en/guide/sync)).

Official docs: [Animator.SetTrigger (Unity)](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Animator.SetTrigger.html), [Animation Parameters (Unity)](https://docs.unity3d.com/2022.3/Documentation/Manual/AnimationParameters.html)
