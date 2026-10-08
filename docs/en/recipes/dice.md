# Dice

Press to roll 1 to 6, shown on a sign and by an animation. Everyone sees the same roll.

## You need

- A button to roll (with a collider)
- A sign for the number (TextMeshPro)
- An Animator with an animation per face, switched by an Int parameter Face

## Variables

| Name | Type | Sync |
|---|---|---|
| face | Integer | Synced |

## Cards

<TriggerCard event="Interact"
  :actions="[{ id: 'Variable.Random', rows: [{ p: 0, value: { ja: '出た目', en: 'face' } }, { p: 1, value: '1', kind: 'number' }, { p: 2, value: '6', kind: 'number' }] }]" />

<TriggerCard event="OnVariableChanged" :name="{ ja: '出た目', en: 'face' }"
  :actions="[
    { id: 'Text.SetText', rows: [{ p: 0, value: 'Dice Sign', kind: 'object' }, { p: 1, value: '{face}', kind: 'text' }] },
    { id: 'Animator.SetInteger', rows: [{ p: 0, value: 'Dice', kind: 'object' }, { p: 1, value: 'Face', kind: 'text' }, { p: 2, value: { ja: '変数: 出た目', en: 'Variable: face' } }] },
  ]" />

For an integer variable, **Random Number** includes the maximum (6).

::: tip The same face twice
Rolling the same face again doesn't change the value, so On Variable Changed doesn't run. To animate every roll, also count rolls in another synced variable and react to that one.
:::
