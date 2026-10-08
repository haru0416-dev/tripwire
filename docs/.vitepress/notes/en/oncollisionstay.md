Happens again and again for each collider touching this one. **What touches it** is a Collision.

A Rigidbody goes to sleep when it moves slower than the Sleep Threshold, and sleeping Rigidbodies don't get this event. So a box that has settled on the floor can stop producing it while still touching.

It needs the same setup as [On Collision Enter](/en/reference/events/oncollisionenter).

Official docs: [OnCollisionStay (Unity)](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.OnCollisionStay.html), [Rigidbody sleeping (Unity)](https://docs.unity3d.com/2022.3/Documentation/Manual/RigidbodiesOverview.html)
