Happens when this object's CharacterController hits a collider while moving with Move. Unity suggests it for pushing objects the character runs into.

A CharacterController only moves when Move is called, so nothing happens unless a script or **Call Udon API** calls Move. For hits on players, use [On Controller Collider Hit Player](/en/reference/events/oncontrollercolliderhitplayer).

**The hit** is a ControllerColliderHit with data about what was hit.

Official docs: [OnControllerColliderHit (Unity)](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.OnControllerColliderHit.html), [Detect Controller Collide (VRChat)](https://creators.vrchat.com/worlds/examples/detect-controller-collide)
