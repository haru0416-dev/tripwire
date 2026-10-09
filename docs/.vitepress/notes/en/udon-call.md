This action is under **Advanced** in Detailed mode. In **Call**, search for any method or property Udon exposes and pick it. The list comes from Udon's own definitions, so members Udon doesn't expose aren't there. Members whose input types Tripwire can't handle yet are left out too.

The SDK's Class Exposure Tree (VRChat SDK > Udon Sharp > Class Exposure Tree) also shows what Udon exposes: green is exposed, red is not.

A member that returns a value can store it in **Store result in**; reading a property needs a variable there. With several targets, the variable keeps the last target's value.

For members with `out` parameters (`int.TryParse`, `Physics.Raycast`, PlayerData's `TryGetInt`...), pick the variable that receives each one in its field, marked **(result)**: it gets the value after the call. When a member returns whether it worked, store that in **Store result in** and branch on it with an If. Members with `ref` parameters (`Mathf.SmoothDamp`...) aren't listed: in Udon their values don't come back.

Members that report back to a behaviour (`VRCStringDownloader.LoadUrl`, `VRCTween`'s versions with a callback, `SendCustomNetworkEvent` with values, `Store.ListPurchases`...) start with **This object** in that field: the result reaches this trigger's events (On String Load Success...) or the Custom event named there. VRCTween's **variableName** takes this trigger's variable name as shown; with another script as the receiver, write that script's own variable name.

Members that change part of a position or color (setting `Vector3.y` or `Color.a`, `Normalize`...) and `VRCTweenHandle`'s `Kill` and the like work only on a variable as the target; the changed value goes back into the variable.

Members returning an enum Udon can't keep in a variable (`VRCPickup.currentHand`, `Renderer.shadowCastingMode`...) store its number in an Integer variable; which number means what shows under the result field.

Calling a method on, or setting a property of, a variable such as a position or a color puts the changed value back into the variable. A synced variable then reaches everyone and On Variable Changed runs.

Calls on objects take effect only on the screen where the action runs ([Other triggers and scripts](/en/guide/linking)).

Official docs: [Class Exposure Tree (VRChat)](https://creators.vrchat.com/worlds/udon/udonsharp/class-exposure-tree)
