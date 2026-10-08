Happens when you switch input method, for example from keyboard to mouse, or from a gamepad to the touchscreen.

**Input method** is a VRCInputMethod value: Keyboard, Mouse, Controller and Touch, plus per-device values such as Vive, Oculus, Index and Pico. Vive is a Vive controller through SteamVR; ViveXR is a Vive XR Elite controller through OpenXR.

Use it to show controls that match how the player is playing. In **Detailed** mode, make a VRCInputMethod variable, fill it with **Set Variable** and compare it in an If.

Official docs: [Input Events (VRChat)](https://creators.vrchat.com/worlds/udon/input-events/), [Type Nodes (VRChat)](https://creators.vrchat.com/worlds/udon/graph/type-nodes/)
