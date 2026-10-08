# Other triggers and scripts

Larger gimmicks don't fit in one trigger. Triggers can call each other and pass values.

## Calling a custom event

A **Custom** event is an event with a name. Another trigger runs it with **Send Event** and that name.

<TriggerCard event="Interact"
  :actions="[{ id: 'Event.Send', rows: [{ p: 0, value: 'Stage', kind: 'object' }, { p: 1, value: 'OpenCurtain', kind: 'text' }] }]" />

<TriggerCard event="Custom" name="OpenCurtain"
  :actions="[{ id: 'Animator.SetTrigger', rows: [{ p: 0, value: 'Curtain', kind: 'object' }, { p: 1, value: 'Open', kind: 'text' }] }]" />

**Send Event Delayed** calls it a few seconds later. UdonSharp scripts can call custom events with `SendCustomEvent` too.

## Another trigger's variables

**Set Another Trigger's Variable** and **Read Another Trigger's Variable** work with another trigger's variables. A change made this way syncs and runs On Variable Changed on that trigger as usual. To call with a value, set the variable first, then send the event.

## Using another script

**Use another script** (under **Other triggers**) calls functions of an UdonSharp script in the scene, or changes its values. Put the object with the script in its target, and **Use** lists what the script offers.

## Notifications from other scripts

The **Notified By Another Script** event receives what scripts such as video players or pens send. Put the object with the script in **Notified by**, then pick **What happened**. Tripwire registers for the notification when the world starts; there is nothing to write by hand.

Tripwire knows the notifications and operations of ProTV, VizVid, USharpVideo and VideoTXL. For other scripts, **Set up by hand…** lets you pick the name and how to register. See [Video players](./video-players).

## Calling Udon directly

Anything not in the lists can be called with **Call Udon API**, under Advanced in Detailed mode: search the methods and properties Udon exposes and pick one.
