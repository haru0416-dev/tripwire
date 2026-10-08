Happens when the player changes certain options in VRChat's graphics settings, such as Field of View or Near Clip Override. It happens on that player's screen only. **Camera settings** is either the screen camera (ScreenCamera) or the handheld camera (PhotoCamera).

Changing VRCCameraSettings values from a script doesn't trigger it, and neither does the camera moving or turning.

It can still happen a lot: every frame while the handheld camera's zoom slider is moving, and every frame while the window is being resized. It can also happen several times in one frame, so keep the actions light.

Official docs: [Event Nodes (VRChat)](https://creators.vrchat.com/worlds/udon/graph/event-nodes/), [VRCCameraSettings (VRChat)](https://creators.vrchat.com/worlds/udon/vrc-graphics/vrc-camera-settings/)
