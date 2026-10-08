Happens when another collider enters this object's area (a collider with Is Trigger on). It is a Unity physics event, called during the physics update (FixedUpdate). **What entered** is the collider that came in.

Both objects need a collider, at least one of them with Is Trigger on, and one of them needs a Rigidbody. In Unity's interaction table, an area with a Rigidbody reacts to any collider. An area without one reacts only to objects with a Rigidbody (a kinematic one is fine).

Unity sends the event both to the area and to the collider that came in, so it also fires on a trigger that isn't the area. Put a trigger on a ball, for example, and it fires when the ball enters the goal's area.

To notice players coming in, use [On Player Trigger Enter](/en/reference/events/onplayertriggerenter).

Official docs: [OnTriggerEnter (Unity)](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.OnTriggerEnter.html), [OnTriggerExit (Unity)](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.OnTriggerExit.html), [Interaction between collider types (Unity)](https://docs.unity3d.com/2022.3/Documentation/Manual/collider-types-interaction.html)
