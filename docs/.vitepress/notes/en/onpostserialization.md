Happens just after an attempt to send the synced variables. **The result** holds success (whether it was sent) and byteCount (how many bytes were sent).

Each send has a size limit: roughly 280,496 bytes with **When changed** and roughly 200 bytes with **All the time (smoothed)**. The per-type sizes in the official docs are approximate, and the data actually sent can be larger. byteCount tells the real amount.

Without a synced variable in this trigger, it never happens.

Official docs: [Network Components (VRChat)](https://creators.vrchat.com/worlds/udon/networking/network-components/), [Networking Specs & Tricks (VRChat)](https://creators.vrchat.com/worlds/udon/networking/network-details/)
