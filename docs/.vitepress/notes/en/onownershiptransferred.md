Happens on every screen in the instance when this object's owner changes. **That player** is the new owner. **Whose** on the card starts as **Me**, so as it is, the actions run only when you become the owner.

In Tripwire, whoever changes a synced variable becomes the owner automatically. So when someone other than the current owner presses a switch, this event happens too. **Take Ownership** also causes it.

The first owner is the first player to join the instance. When the owner leaves, VRChat picks a new owner automatically.

Official docs: [Network Components (VRChat)](https://creators.vrchat.com/worlds/udon/networking/network-components/), [Object Ownership (VRChat)](https://creators.vrchat.com/worlds/udon/networking/ownership/)
