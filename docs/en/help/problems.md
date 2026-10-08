# Errors and warnings

A card with a problem says so with the color of its frame and a message inside.

- Red frame (error): it can't be converted, and the trigger isn't applied until it is fixed
- Orange frame (warning): it converts, but may not do what you expect

The status at the top of the Inspector counts problems. **Show them** opens only the cards with problems and folds the others. On a folded card, hover the red or orange icon with a number in its header to read why. The state of every trigger in the scene is in the trigger list (the list button at the right of the header).

## Common ones

| Message | Fix |
|---|---|
| No objects assigned; this action does nothing. | Drag an object into its target |
| Clicking needs a collider on this object. | Press **Add a collider** on the card |
| This event needs an area: a collider with Is Trigger on. | Press **Add an area** on the card |
| Pick a variable. | Pick a variable in the action; make one with **+ Variable** first |
| Setting a synced variable from a broadcast event makes every player take ownership at once… | Set the card back to **Only my screen** in Advanced settings; the value reaches everyone anyway |
| Objects and players cannot be synced… | Sync a number that tells which one, and pick by it |
| A Custom event already uses this name. | Rename one of them |
| Pick one of this trigger's timers… | Name the timer event and pick that name |
| A '{' without '}'… | To write `{` as a character, double it |

## "Use Only my screen"

Every-frame events, timers, On Variable Changed and others already run on every player's screen. Sending them to everyone's screen repeats them once per player or floods the network. When warned, set the card back to **Only my screen**.
