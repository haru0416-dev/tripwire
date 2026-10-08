# Trying it in Play

When you press Play, Tripwire runs your triggers in ClientSim. The Inspector during Play and the Event History show what ran and what stopped.

## On each card: when it last ran

During Play, each card shows under its title when it last ran and how many times. When its conditions stopped it, the card says which condition didn't hold and what the variables were at that moment.

- Ran: `Ran at 4.37s (3 times)`
- Stopped: `7.44s: Condition not met: count less than 3 (count = 3)`

The card's frame lights up green for a moment when it runs, and orange when it is stopped. An On Variable Changed card also shows the new value.

## Event History

"Open the event history" (or Tools > Tripwire > Event History) lists the events that ran or were stopped during this Play, newest first. Click a row to select the object; "Selected only" keeps the triggers on the selected objects. The history stays until you press Play again.

Events that happen many times a second (every frame, while staying in an area) are not recorded. When records come faster than they can be read, the window says how many were missed.

## Changing values to try things

The values under "Variables now" can be changed during Play; numbers and text take effect when you press Enter. A change works as if an action had made it: a synced variable is sent to everyone, and its On Variable Changed events run.

▶ next to an event runs it as if it happened. When its conditions aren't met, it doesn't run and is recorded as stopped.

## Effect on your world

The recording is part of the generated script, but stays off until Tripwire turns it on during Play. In VRChat it costs one on/off check where an event runs, and records nothing.
