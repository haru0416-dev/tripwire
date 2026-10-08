Happens when the slider in the card's **UI element** moves. It is wired like a button: on conversion Tripwire adds a call in the slider's On Value Changed ([UI Button Pressed](/en/reference/events/uibuttonclick)). While the handle is dragged, it fires each time the value changes.

**The event's value** is the slider's value, from its Min Value to its Max Value. It arrives as a number with decimals even with Whole Numbers on.

If moving keys or sticks nudge the slider, set its Navigation to None. Unity's Slider also has a separate SetValueWithoutNotify that changes the value without calling On Value Changed.

Official docs: [Slider (Unity UI)](https://docs.unity3d.com/Packages/com.unity.ugui@1.0/manual/script-Slider.html), [Slider API (Unity UI)](https://docs.unity3d.com/Packages/com.unity.ugui@1.0/api/UnityEngine.UI.Slider.html), [VRC Ui Shape (VRChat)](https://creators.vrchat.com/worlds/components/vrc_uishape/)
