Happens only on a trigger attached to the same object as a Camera, just before that camera works out what it can see (culling). Changing the camera's settings here changes what it sees.

Worlds can't access the player's own view camera. This event is for cameras you place in the world yourself, such as one rendering to a Render Texture.

Official docs: [MonoBehaviour.OnPreCull (Unity)](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.OnPreCull.html), [VRCCameraSettings (VRChat)](https://creators.vrchat.com/worlds/udon/vrc-graphics/vrc-camera-settings/)
