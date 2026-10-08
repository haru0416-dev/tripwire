# The same state for everyone

In VRChat, gimmicks run on each player's own screen. Left alone, when one player turns a light off, the light stays on for everyone else. Sync is how everyone sees the same state.

## Which to use

| You want | Use |
|---|---|
| It to happen only for whoever did it (teleporting, a personal effect) | nothing more (Only my screen) |
| Everyone to keep seeing the same state (lights, doors, scores) | a synced variable and On Variable Changed |
| Everyone present to see something once (a sound, an effect) | **Everyone's screen** in Advanced settings |

When unsure, use a synced variable. Only synced variables reach players who join later; actions sent to everyone's screen run only for the players there at that moment.

## Synced variables

Set a variable to **Synced** and its value reaches everyone. Change it with ordinary actions (Toggle Variable, Add To Variable...). Whoever changes it becomes its owner automatically and sends it to everyone.

To act when a value arrives, use the **On Variable Changed** event. It runs when you change the value, when someone else does, and when a player who joins later receives the current value.

<TriggerCard event="OnVariableChanged" :name="{ ja: 'ライト', en: 'light' }"
  :actions="[{ id: 'GameObject.SetActive', rows: [{ p: 0, value: 'Lamp', kind: 'object' }, { p: 1, value: { ja: '変数: ライト', en: 'Variable: light' } }] }]" />

For a full example, see [A switch everyone shares](/en/recipes/shared-switch).

## Watch out for

::: warning Change synced variables from your own screen only
If an action that changes a synced variable runs on everyone's screen, every player tries to become the owner at once and players end up with different values. The change reaches everyone anyway, so changing it on the screen of whoever pressed is enough. Tripwire gives such a card an orange frame and a warning message.
:::

- Objects and players can't be synced: sync a number that tells which one, and pick by that number
- Changing a variable inside its own On Variable Changed runs the event again each time
- Every-frame events and timers sent to everyone's screen go over the network each time (Tripwire warns about this)

## How synced values are sent

In Detailed mode, a trigger with synced variables shows **Send synced**. Keep **When changed** for most gimmicks. Use **All the time (smoothed)** only for values that change many times a second, like a position that keeps moving.
