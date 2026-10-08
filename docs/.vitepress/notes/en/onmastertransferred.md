Happens on every screen when the instance master changes. The master changes when the previous master leaves. It can also change when someone on Android keeps VRChat in the background for too long.

The new master is chosen before On Player Left runs. The server picks them based on various criteria, so don't count on any particular player. For the first player in a new instance, this event happens after On Player Joined.

**Whose** on the card starts as **Me**, so the actions run only when you become master. The official docs advise against using master status to gate features, and recommend checking ownership instead where possible: a master can become unresponsive for a while, and events during that time may not run.

Official docs: [Network Components (VRChat)](https://creators.vrchat.com/worlds/udon/networking/network-components/), [Object Ownership (VRChat)](https://creators.vrchat.com/worlds/udon/networking/ownership/)
