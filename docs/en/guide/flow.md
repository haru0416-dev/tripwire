# If, loops, timers

Actions run from top to bottom. To branch, repeat or wait, use the actions in the **If, loops, timers** category.

## Branching with If

If runs one list of actions when its conditions hold (Then) and another when they don't (Else). Put an If inside Else for Else If.

<TriggerCard event="Interact"
  :actions="[{ id: 'Flow.If', rows: [{ label: { ja: '条件', en: 'Condition' }, value: { ja: '点数 が 10 以上 のとき', en: 'score is at least 10' }, kind: 'text' }],
    then: [{ id: 'GameObject.SetActive', rows: [{ p: 0, value: 'Prize', kind: 'object' }, { p: 1, value: { ja: 'オン', en: 'On' } }] }],
    else: [{ id: 'Variable.Add', rows: [{ p: 0, value: { ja: '点数', en: 'score' } }, { p: 1, value: '1', kind: 'number' }] }] }]" />

Conditions use variables. Number variables can be compared, and several conditions can be combined with all or any one. For a condition on the whole card, use the conditions in the card's Advanced settings instead of If.

## Loops

| Action | Repeats |
|---|---|
| Repeat | a set number of times |
| For Each | once for each item of a list variable |
| While | while its conditions hold |

For Each is how you act on a list of objects: put actions that target the item variable under Do, and they run for every item.

::: warning Make While end
If nothing under While changes the variable in its condition, the loop never ends and the trigger stops working.
:::

Inside loops, Break leaves the loop and Continue skips to the next round. Return skips the rest of the event's actions.

## Timers

The **Timer** event runs every few seconds.

- Every: the seconds; with **Random**, a different time between the shortest and longest each round
- Runs: repeatedly or once
- Start right away: when off, it waits for a **Start Timer** action

Give the timer a name, and pick that name in **Start Timer** and **Stop Timer**. Timers keep running when the object is hidden; to stop one then, put **Stop Timer** in **OnDisable** (under Advanced in Detailed mode).

## Delays

**Delay** in a card's Advanced settings runs its actions a few seconds after the event. **Send Event Delayed** calls a named event a few seconds later.
