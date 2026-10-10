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

## Not synced, synced, temporary, saved

Next to the initial value, pick whether it syncs, or make it temporary or saved.

- Not synced: each player has a value of their own, kept until it is changed
- Synced: everyone gets the same value, players who join later too
- Temporary: starts from its initial value each time an event runs; other triggers can't see it
- Saved: each player's value is saved and given back when they come again

How syncing works and when to use it: [The same state for everyone](./sync).

A **Saved** variable keeps each player's value in VRChat's PlayerData: coins, or the brightness picked on a settings panel. It comes back when that player's data is loaded ([On Player Restored](/en/reference/events/onplayerrestored)), and On Variable Changed runs. Changes made before then aren't saved.

On/off, numbers, text, positions, colors and rotations can be saved; a saved variable can't also be synced or temporary. Its **Save name** starts as the variable's name when **Saved** was picked and can be changed under the variable; renaming the variable later keeps it. Triggers that save under the same name share one value.

If the trigger's object is hidden when the player joins, the saved values may not come back: keep saved variables on objects shown from the start.

Each change to a saved variable sends all of that player's saved data. Changing one many times a second (every frame, for example) gets a warning: change it on a less frequent event.

## Changing values

The actions in the Variable category change a variable's value.

| Action | Does |
|---|---|
| [Set Variable](/en/reference/actions/variable-set) | Puts in a value, another variable or an event value |
| [Toggle Variable](/en/reference/actions/variable-toggle) | Flips on/off |
| [Add To Variable](/en/reference/actions/variable-add) | Adds a number (negative to subtract) |
| [Random Number](/en/reference/actions/variable-random) | Puts in a random number from min to max |
| [Calculate](/en/reference/actions/variable-calculate) | Puts in A `+ − × ÷ %` B (positions and colors too; text joins) |
| [Get Component](/en/reference/actions/variable-getcomponent) | Puts an object's component into a variable of that type |

A **Call Udon API** result can go into a variable too. However it changes, a synced variable reaches everyone and On Variable Changed runs.

## Values in text

In **Set Text** and **Log**, write a variable's name in braces, like `{score}`, and it becomes the variable's current value: `Score: {score}` shows "Score: 12". The field takes several lines, so line breaks can be typed in.

Any text can also show these without a variable (a variable of the same name comes first):

| Write | Shows |
|---|---|
| `{playerCount}` (`{プレイヤー数}`) | How many are in the instance |
| `{myName}` (`{自分の名前}`) | Your display name |
| `{time}` (`{時刻}`) | The time now (24-hour hh:mm) |
