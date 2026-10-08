# A switch everyone shares

Whoever presses it turns a light on or off for everyone. Players who join later get the current state.

## You need

- An object to be the switch (with a collider)
- The light to switch

## Variables

| Name | Type | Sync |
|---|---|---|
| switch | On/Off | Synced |

## Cards

<TriggerCard event="Interact"
  :actions="[{ id: 'Variable.Toggle', rows: [{ p: 0, value: { ja: 'スイッチ', en: 'switch' } }] }]" />

<TriggerCard event="OnVariableChanged" :name="{ ja: 'スイッチ', en: 'switch' }"
  :actions="[{ id: 'GameObject.SetActive', rows: [{ p: 0, value: 'Lamp', kind: 'object' }, { p: 1, value: { ja: '変数: スイッチ', en: 'Variable: switch' } }] }]" />

The first card flips the variable on the screen of whoever pressed; the variable is synced, so the value reaches everyone. The second card sets the light from the value on every screen it reaches.

The starter **A switch everyone shares**, shown while the trigger is empty, makes the variable and both cards.

## Why two cards

With **Set Active** directly in the first card, the light changes only for whoever pressed. Reacting to the value with On Variable Changed works the same for the presser, everyone else and players who join later. More in [The same state for everyone](/en/guide/sync).
