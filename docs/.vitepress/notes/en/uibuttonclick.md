Happens when the button in the card's **UI element** is pressed. When the trigger is converted (before Play and builds, or with **Apply now**), Tripwire adds a call to the trigger in the button's On Click. Tripwire only touches the call it added; anything else in On Click stays as it is.

A Unity button runs On Click when it is pressed and released. Moving the pointer off the button before releasing cancels it.

Without a VRC Ui Shape on the Canvas, the card shows a warning with an **Add VRC Ui Shape** button. If the Canvas is still on the UI layer, players can press it only while the VRChat menu is open; move it to Default or another layer.

The actions run on the screen of whoever pressed the button. To show the result to everyone, change a synced variable and react with On Variable Changed ([The same state for everyone](/en/guide/sync)).

Official docs: [VRC Ui Shape (VRChat)](https://creators.vrchat.com/worlds/components/vrc_uishape/), [UI Events (VRChat)](https://creators.vrchat.com/worlds/udon/ui-events/), [Button (Unity UI)](https://docs.unity3d.com/Packages/com.unity.ugui@1.0/manual/script-Button.html)
