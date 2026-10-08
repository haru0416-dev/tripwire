# Mirror switch

A switch that shows and hides a mirror when clicked, for whoever clicks it.

## You need

- An object to be the switch (with a collider)
- A mirror (such as VRCMirror from the VRChat SDK)

## Cards

Add a Tripwire Trigger to the switch and make this card.

<TriggerCard event="Interact"
  :actions="[{ id: 'GameObject.ToggleActive', rows: [{ p: 0, value: 'Mirror', kind: 'object' }] }]" />

The starter **Click to show / hide**, shown while the trigger is empty, makes this card; then put the mirror in **Targets**.

## Variations

- To switch the mirror for everyone at once, build it like [A switch everyone shares](./shared-switch)
- To switch between two mirrors (high and low quality), put both in **Targets** and hide one from the start
