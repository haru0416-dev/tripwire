Picks one object of **List** at random and puts it into an Object variable. Drag objects into the list, or pick an object list variable. The variable then works as any action's target.

For example, list some destinations and [Teleport](/en/reference/actions/player-teleport) to the one picked for a different place each time, or show just one prize of a raffle.

An empty list leaves the variable as it was. Each screen picks for itself: objects can't be synced, so to pick the same one for everyone, put a synced number with [Random Number](/en/reference/actions/variable-random) and branch on it.

Official docs: [Random.Range (Unity)](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Random.Range.html)
