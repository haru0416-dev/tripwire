Happens once on each player's screen, when the object starts. It happens on the screens of players who join later too.

It suits per-player setup (changing movement speed, for example). A starting state everyone shares is more reliable as a synced variable's initial value than set up here.

Unity calls Start exactly once, on the frame the script is enabled, just before the first Update. An object that starts hidden doesn't get it until it is first shown.

Timers with **Start right away** on also start at this moment. Each player counts from when they joined, so timers run out of step between players ([Timer](/en/reference/events/timer)).

With **Everyone's screen** in the advanced settings, each player who joins runs the actions again on every screen. Keep it on **Only my screen**.

In Play mode, the Inspector's **▶ Run** doesn't offer this event: running it again would start timers twice.

Official docs: [Start (Unity)](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.Start.html)
