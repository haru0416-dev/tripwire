Happens just before the synced variables are sent out. The official docs call it a good place to set synced values you want other players to get.

With **When changed**, changing values in quick succession doesn't send each time: VRChat waits until enough time has passed since the last send. Then it runs this event, sends, and runs On Post Serialization. The more data an object sends, the longer it has to wait between sends.

Tripwire sends a synced variable automatically when you change it, so you usually don't need this event. Without a synced variable in this trigger, it never happens.

Official docs: [Network Components (VRChat)](https://creators.vrchat.com/worlds/udon/networking/network-components/), [Networking Specs & Tricks (VRChat)](https://creators.vrchat.com/worlds/udon/networking/network-details/)
