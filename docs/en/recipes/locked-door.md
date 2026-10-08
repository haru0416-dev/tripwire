# Locked door

A door only someone holding the key can open. Clicking without the key shows "You need the key" on a sign.

## You need

- A key (an object with VRC Pickup)
- The door (clicked) and the door panel to hide
- A sign for the message (TextMeshPro)

## The door's trigger

Add a Tripwire Trigger to the door with this variable.

| Name | Type | Sync |
|---|---|---|
| has key | On/Off | Not synced |

<TriggerCard event="Interact"
  :actions="[{ id: 'Flow.If', rows: [{ label: { ja: '条件', en: 'Condition' }, value: { ja: '鍵を持っている が オン のとき', en: 'has key is On' }, kind: 'text' }],
    then: [{ id: 'GameObject.SetActive', rows: [{ p: 0, value: 'Door Panel', kind: 'object' }, { p: 1, value: { ja: 'オフ', en: 'Off' } }] }],
    else: [{ id: 'Text.SetText', rows: [{ p: 0, value: 'Sign', kind: 'object' }, { p: 1, value: { ja: '鍵が必要です', en: 'You need the key' }, kind: 'text' }] }] }]" />

## The key's trigger

Add a Tripwire Trigger to the key that sets the door's variable when picked up.

<TriggerCard event="OnPickup"
  :actions="[{ id: 'Trigger.SetVariable', rows: [{ p: 0, value: 'Door', kind: 'object' }, { p: 1, value: { ja: '鍵を持っている', en: 'has key' } }, { p: 2, value: { ja: 'オン', en: 'On' } }] }]" />

The variable isn't synced, so it turns on only for the player who picked up the key, and the door opens only on their screen.

## Opening it for everyone

Keep "has key" as it is, and keep the door's state in a separate synced variable: set it under Then, and hide the panel from On Variable Changed, as in [A switch everyone shares](./shared-switch).
