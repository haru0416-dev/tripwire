The card sets the timer's name, the interval in seconds, **Random**, repeatedly or once, and **Start right away**. More in [If, loops, timers](/en/guide/flow#timers).

Timers run on each player's screen, so with a random interval the timing differs between players. For something everyone should see in step, let the timer change a synced variable and react with On Variable Changed.

Even with a fixed interval, players hear it at different times: a timer with **Start right away** starts on each player's screen together with Start, when that player loads in. Tripwire builds timers on VRChat's SendCustomEventDelayedSeconds, which runs an event after a number of seconds.

Restarting a running timer with Start Timer waits a full interval from that moment. A timer set to **Once** stops after it fires.

Starting a timer from an event that happens many times a second (On Player Trigger Stay, for example) restarts it every time, so it never fires.

Official docs: [SendCustomEventDelayedSeconds (UdonSharp)](https://udonsharp.docs.vrchat.com/vrchat-api/)
