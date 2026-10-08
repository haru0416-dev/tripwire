# FAQ

## Where are the generated scripts?

In `Assets/TripwireGenerated`. Keep the folder, and commit it to version control with its .meta files: the scene's components point at those files, and recreated ones would be new files the scene doesn't know. They are plain UdonSharp you can open and read; **Code** in the Inspector opens a trigger's script.

## Do visitors of my world need anything?

No. The scripts are plain UdonSharp, and VRChat needs nothing Tripwire-specific.

## Can the editor be in English?

Yes: **Language** in the ⋮ menu at the top of the Inspector switches between Japanese and English. Your cards don't change.

## The event I want isn't in the picker

Set the Inspector to **Detailed**: physics, avatars, input, sync data, every-frame events and more show up. If it's still missing, search for it in [Events](/en/reference/events).

## Can I combine it with scripts from other creators?

Functions of UdonSharp scripts can be called with **Use another script**, and their notifications received with **Notified By Another Script**. See [Other triggers and scripts](/en/guide/linking).

## It works in Unity but not in VRChat

First check that ClientSim behaves the same. When ClientSim works and VRChat doesn't, it's usually about sync: see [The same state for everyone](/en/guide/sync). If that doesn't help, please open an issue.

## Nothing happens when I press it

During Play each card shows when it last ran, or which condition stopped it. The Event History lists what ran. See [Trying it in Play](/en/guide/testing).

## Can I sell or share gimmicks I made?

Yes. For people without Tripwire, use Export for Distribution; passed on as is, the Tripwire component becomes a Missing Script for them. See [Distributing and selling](/en/guide/distribution).
