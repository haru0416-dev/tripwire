Calls the target's Custom event once **Seconds** have passed. **Event name** works as in [Send Event](/en/reference/actions/event-send). Even at 0 seconds it doesn't run on the spot: it runs in the same frame or the next one.

It runs only on the screen where the action runs. To run something on everyone's screen, use **Send Event** with **Everyone's screen** inside the Custom event it calls.

To delay a whole card's actions, the card's **Delay** in Advanced settings is enough. For a steady interval, a Timer fits better ([If, loops, timers](/en/guide/flow#delays)).

Official docs: [Special Nodes (VRChat)](https://creators.vrchat.com/worlds/udon/graph/special-nodes)
