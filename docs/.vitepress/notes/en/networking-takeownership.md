Makes the player on whose screen the action runs the owner of the target objects. Each object has one owner, and only the owner can change that object's synced variables. At first, the owner is the first player in the instance.

Tripwire's synced variables already make whoever changes them the owner, so variables don't need this action. Use it to receive events sent with **Owner's screen**, or for other scripts that expect the owner to act.

When the owner changes, [On Ownership Transferred](/en/reference/events/onownershiptransferred) happens on the screen of everyone in the instance. If the owner leaves, VRChat picks a new owner by itself. If a script on the object refuses in OnOwnershipRequest, ownership doesn't move.

Official docs: [Object Ownership (VRChat)](https://creators.vrchat.com/worlds/udon/networking/ownership/), [Network Components (VRChat)](https://creators.vrchat.com/worlds/udon/networking/network-components/)
