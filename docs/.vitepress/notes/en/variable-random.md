With 1 to 6, 6 can come up too.

Into a synced variable, the result reaches everyone: dice and lotteries everyone should agree on are built this way ([Dice](/en/recipes/dice)).

For a Number variable, both the minimum and the maximum can come up. For whole numbers, Unity's Random.Range leaves out the maximum, so Tripwire adds 1 to it when calling. If both are typed in and the minimum is larger, it's an error.

Official docs: [Random.Range (Unity)](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Random.Range.html)
