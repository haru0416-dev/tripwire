# What is Tripwire

Tripwire is a Unity editor extension for building VRChat world gimmicks in the Inspector. On a Tripwire Trigger attached to an object, you pick **when** something happens (an event) and list **what to do** (actions). Tripwire turns that into an UdonSharp script, and it runs in Udon as it is.

<TriggerCard event="OnPlayerTriggerEnter"
  :actions="[{ id: 'AudioSource.Play', rows: [{ p: 0, value: 'Chime', kind: 'object' }] }]" />

This one card says "when a player walks in, play the Chime". A gimmick is a few such cards, each with as many actions as it needs.

## What it can do

- **When**: clicks, entering and leaving an area, pickups, players joining, UI buttons, video playback, and almost every other event UdonSharp has
- **What to do**: show and hide, move, Animator, sound, text, teleport, video players, variables. Anything else Udon exposes can be searched and called
- **Flow**: If branches, Repeat / For Each / While loops, and timers
- **Variables and sync**: on/off, numbers, text, objects and more. Synced variables reach everyone, including players who join later
- **Other assets**: playback events and common operations of ProTV, VizVid, USharpVideo and VideoTXL

The full lists are in [Events](/en/reference/events) and [Actions](/en/reference/actions).

## Tripwire and CyanTrigger

Tripwire was started as a replacement for CyanTrigger, which let people build gimmicks without code and is no longer updated. It is written from scratch and contains no CyanTrigger code. It does not import CyanTrigger triggers.

## What it makes

The scripts it writes are plain UdonSharp. VRChat needs nothing Tripwire-specific, and neither do visitors of your world. The scripts go to `Assets/TripwireGenerated` and are rewritten when needed.
