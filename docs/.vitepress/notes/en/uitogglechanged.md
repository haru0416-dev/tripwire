Happens when the toggle in the card's **UI element** is switched. It is wired like a button: on conversion Tripwire adds a call in the toggle's On Value Changed ([UI Button Pressed](/en/reference/events/uibuttonclick)).

**The event's value** is the toggle's isOn, read when the event fires, so it is the state after the switch. Pick it for **Show** in **Set Active** and the object follows the toggle. To do different things for on and off, put it into a variable with **Set Variable** and branch with If.

Unity's Toggle has a separate SetIsOnWithoutNotify for changing the value without calling On Value Changed. So setting isOn directly, even from a script, fires this event too.

Official docs: [Toggle (Unity UI)](https://docs.unity3d.com/Packages/com.unity.ugui@1.0/manual/script-Toggle.html), [Toggle API (Unity UI)](https://docs.unity3d.com/Packages/com.unity.ugui@1.0/api/UnityEngine.UI.Toggle.html), [VRC Ui Shape (VRChat)](https://creators.vrchat.com/worlds/components/vrc_uishape/)
