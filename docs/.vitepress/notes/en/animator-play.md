Switches straight to the state named in **State** and plays it, without waiting for a transition's conditions. Use the state's name from the Animator window. Unity's docs recommend including the layer, like `Base Layer.Open`.

The action gives no layer, so the state plays in the first layer found to have that name. If several layers have a state with the same name, add the layer name to tell them apart.

It switches only on the screen where the action runs, and hiding the object clears the Animator's current state by default. For a state everyone and later joiners should see, a synced variable with **Animator Set Bool** is more reliable ([The same state for everyone](/en/guide/sync)).

Official docs: [Animator.Play (Unity)](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Animator.Play.html), [Animator.keepAnimatorStateOnDisable (Unity)](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Animator-keepAnimatorStateOnDisable.html)
