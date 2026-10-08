Happens once per physics update for every collider inside the area: with three objects inside, it runs three times per update. **What is inside** is the collider for that call.

It needs the same setup as On Trigger Enter: one of the two needs a Rigidbody ([On Trigger Enter (Collider)](/en/reference/events/ontriggerenter)). If entering and leaving are enough, that event and On Trigger Exit are lighter.

Official docs: [OnTriggerStay (Unity)](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.OnTriggerStay.html)
