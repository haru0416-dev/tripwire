Puts a number into a Float parameter of the Animator Controller. Besides transition conditions, it can drive the blend of a Blend Tree. Use the same name as the Animator's parameter.

The parameter takes the new value at once. **Value** can be a number variable or a value the event brings: pass in the value of UI Slider Moved to drive an animation with a slider.

Only the Animator on the screen where the action runs changes. To keep everyone in step, react to a synced number variable with On Variable Changed and run this action there ([The same state for everyone](/en/guide/sync)).

Official docs: [Animator.SetFloat (Unity)](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Animator.SetFloat.html), [Animation Parameters (Unity)](https://docs.unity3d.com/2022.3/Documentation/Manual/AnimationParameters.html)
