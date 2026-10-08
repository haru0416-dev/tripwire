Puts a whole number into an Int parameter of the Animator Controller. Compared in transition conditions, one parameter can pick between three or more states, which is simpler than juggling several Bools. Use the same name as the Animator's parameter.

**Value** can be an integer variable. React to a synced integer variable with On Variable Changed and pass it into **Value** to share states like three light levels with everyone ([The same state for everyone](/en/guide/sync)). On its own, the action changes only the Animator on the screen where it runs.

Official docs: [Animator.SetInteger (Unity)](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Animator.SetInteger.html)
