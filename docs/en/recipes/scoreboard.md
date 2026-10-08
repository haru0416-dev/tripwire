# Scoreboard

Each press of a button adds a point, shown on a sign. The score is shared and reaches players who join later.

## You need

- An object to be the button (with a collider)
- A sign for the score (TextMeshPro)

## Variables

| Name | Type | Sync |
|---|---|---|
| score | Integer | Synced |

## Cards

<TriggerCard event="Interact"
  :actions="[{ id: 'Variable.Add', rows: [{ p: 0, value: { ja: 'スコア', en: 'score' } }, { p: 1, value: '1', kind: 'number' }] }]" />

<TriggerCard event="OnVariableChanged" :name="{ ja: 'スコア', en: 'score' }"
  :actions="[{ id: 'Text.SetText', rows: [{ p: 0, value: 'Score Board', kind: 'object' }, { p: 1, value: { ja: 'スコア: {スコア}点', en: 'Score: {score}' }, kind: 'text' }] }]" />

`{score}` in the text becomes the variable's current value.

## Variations

- A reset button: another button with **Set Variable** setting score to 0
- A minus button: **Add To Variable** with -1
