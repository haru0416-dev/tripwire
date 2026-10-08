Turns a Bool parameter of the Animator Controller on or off. With the Bool in transition conditions, the state decides the motion: on opens, off closes. Use the same name as the Animator's parameter.

**Value** can be an On/Off variable. React to a synced variable with On Variable Changed and pass that variable straight into **Value**: doors and lights then match on every screen, and players who join later reach the same state when the variable arrives ([The same state for everyone](/en/guide/sync)).

Hiding the object clears the Animator's current state by default. To keep it, turn on Keep Animator State On Disable on the Animator.

Official docs: [Animator.SetBool (Unity)](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Animator.SetBool.html), [Animator.keepAnimatorStateOnDisable (Unity)](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Animator-keepAnimatorStateOnDisable.html)
