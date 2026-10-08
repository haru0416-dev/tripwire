Happens when this object's collider starts touching another collider. Both must have Is Trigger off, and at least one needs a Rigidbody with Is Kinematic off. So nothing happens between a kinematic object and a floor without a Rigidbody.

**What hit it** is a Collision: data about the contact points, the impact speed and so on.

The trigger can sit on either side. When a box with a Rigidbody falls onto the floor, it fires on a trigger on the floor and on a trigger on the box.

To notice players hitting it, use [On Player Collision Enter](/en/reference/events/onplayercollisionenter).

Official docs: [OnCollisionEnter (Unity)](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.OnCollisionEnter.html), [Interaction between collider types (Unity)](https://docs.unity3d.com/2022.3/Documentation/Manual/collider-types-interaction.html)
