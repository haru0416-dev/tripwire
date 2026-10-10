Runs one of the Custom events named in **Events**, picked at random. Write the names one per line, or separated by commas: for raffles, or a different effect each time.

It picks once, so every target gets the same event. Who runs it works as in [Send Event](/en/reference/actions/event-send): with **Everyone's screen** (All), the pick happens once on the screen where the action runs and the chosen event runs for everyone. Late joiners don't get it; keep a result that should last in a synced variable.

With this trigger as the target, a name it has no Custom event for gets a warning. Every name is equally likely.

Official docs: [Random.Range (Unity)](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Random.Range.html), [Network Events (VRChat)](https://creators.vrchat.com/worlds/udon/networking/events/)
