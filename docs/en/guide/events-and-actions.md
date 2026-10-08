# When → What to do

A Tripwire Trigger is a list of cards. Each card has one "when" and the actions to run then.

## Reading a card

<TriggerCard event="OnPlayerTriggerEnter"
  :actions="[
    { id: 'GameObject.SetActive', rows: [{ p: 0, value: 'Light_A', kind: 'object' }, { p: 1, value: { ja: 'オン', en: 'On' } }] },
    { id: 'AudioSource.Play', rows: [{ p: 0, value: 'Chime', kind: 'object' }] },
  ]" />

- The colored line on the left and the icon show the category; the same color means the same kind of thing
- Next to the **When** label is the event. Press it to pick another
- Actions run from top to bottom

Drag cards and actions by the ≡ on their left. The ⋯ menu duplicates, copies and pastes, adds a note, or deletes.

## Values an event brings

Some events bring values, like the player who walked in. Action settings can use them as a target or a value: with that player as the target of **Teleport Local Player**, only the player who walked in moves.

What each event has is listed under "Event values" on its page in [Events](/en/reference/events).

## Simple and Detailed

**Simple / Detailed** at the top of the Inspector sets how much the pickers show.

- **Simple**: the events and actions most gimmicks use
- **Detailed**: every Udon event (physics, avatars, input, sync data, every frame...), with their Udon names such as `OnPlayerTriggerEnter`

Cards you made stay the same either way.

## Conditions

Open a card's **Advanced settings** to add conditions: the card's actions run only when they hold. Choose all or any one, and "not" for each.

To branch in the middle of the actions, use If.

<TriggerCard event="Interact"
  :actions="[{ id: 'Flow.If',
    then: [{ id: 'GameObject.ToggleActive', rows: [{ p: 0, value: 'Door', kind: 'object' }] }],
    else: [{ id: 'Text.SetText', rows: [{ p: 0, value: 'Sign', kind: 'object' }, { p: 1, value: { ja: '鍵が必要です', en: 'You need the key' }, kind: 'text' }] }] }]" />

## Whose screen

**Advanced settings** also sets whose screen runs the actions. **Only my screen** runs them for the player who clicked; **Everyone's screen** for all. To keep a state everyone shares, a [synced variable](./variables) is more reliable.
