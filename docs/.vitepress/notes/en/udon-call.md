This action is under **Advanced** in Detailed mode. In **Call**, search for any method or property Udon exposes and pick it. The list comes from Udon's own definitions, so members Udon doesn't expose aren't there. Members whose input types Tripwire can't handle yet are left out too.

The SDK's Class Exposure Tree (VRChat SDK > Udon Sharp > Class Exposure Tree) also shows what Udon exposes: green is exposed, red is not.

A member that returns a value can store it in **Store result in**; reading a property needs a variable there. With several targets, the variable keeps the last target's value.

Calling a method on, or setting a property of, a variable such as a position or a color puts the changed value back into the variable. A synced variable then reaches everyone and On Variable Changed runs.

Calls on objects take effect only on the screen where the action runs ([Other triggers and scripts](/en/guide/linking)).

Official docs: [Class Exposure Tree (VRChat)](https://creators.vrchat.com/worlds/udon/udonsharp/class-exposure-tree)
