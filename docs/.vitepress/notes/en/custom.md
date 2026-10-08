Other triggers call it by its **Name** with **Send Event**, and UdonSharp scripts with `SendCustomEvent`. One trigger can't use the same name twice.

It also works as a shared list of actions: put them here once and call it from several cards.

Names start with a letter and use only A-Z, a-z, 0-9 and _. Names Udon already uses (Start and so on) and names starting with Tw_, tw_ or v_ are not allowed.

When Send Event's **Run on** is **Everyone's screen** or **Owner's screen**, the call goes out as a VRChat network event. Network events never reach players who join later. By default each event can be sent 5 times a second; extra calls wait in a queue on the sender. For a state everyone should share, use [synced variables](/en/guide/sync).

[Other triggers and scripts](/en/guide/linking#calling-a-custom-event) shows how to call it.

Official docs: [Custom Network Events (VRChat)](https://creators.vrchat.com/worlds/udon/networking/events), [SendCustomEvent (UdonSharp)](https://udonsharp.docs.vrchat.com/vrchat-api/)
