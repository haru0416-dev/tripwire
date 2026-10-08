Happens when a collider that was inside the area (a collider with Is Trigger on) leaves it. **What left** is that collider. Often used to undo what entering changed.

It needs the same setup as On Trigger Enter: one of the two needs a Rigidbody. Unity sends it both to the area and to the collider that left ([On Trigger Enter (Collider)](/en/reference/events/ontriggerenter)).

Official docs: [OnTriggerExit (Unity)](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.OnTriggerExit.html), [Interaction between collider types (Unity)](https://docs.unity3d.com/2022.3/Documentation/Manual/collider-types-interaction.html)
