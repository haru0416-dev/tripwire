# Variables

A variable is a value the trigger remembers: whether the door is open, how many times it was pressed, the score.

## Making a variable

Press **+ Variable**, give it a name and pick its type.

| Type | Holds | For |
|---|---|---|
| On/Off | on or off | a door is open, a light is lit |
| Integer | a whole number | counts, scores |
| Number | a number with decimals | time, volume |
| Text | text | names, messages |
| Object | one object of the scene | what to move |
| Object list | several objects | lights to switch together |
| Player | one player | who pressed last |

More types are in **Detailed** mode.

## Not synced, synced, temporary

Next to the initial value, pick whether it syncs, or make it temporary.

- Not synced: each player has a value of their own, kept until it is changed
- Synced: everyone gets the same value, players who join later too
- Temporary: starts from its initial value each time an event runs; other triggers can't see it

How syncing works and when to use it: [The same state for everyone](./sync).

## Values in text

In **Set Text** and **Log**, write a variable's name in braces, like `{score}`, and it becomes the variable's current value: `Score: {score}` shows "Score: 12".
