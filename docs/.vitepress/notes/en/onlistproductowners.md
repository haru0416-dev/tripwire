Happens as the answer to a Store.ListProductOwners call. **Owners** holds the display names of everyone who owns the product, not just the players in this instance.

It happens only if "Owners Names in Udon" is turned on for the product on VRChat.com. When testing locally, the placeholder names VRCat, Fred and VRRat come in place of real names. With several UdonBehaviours on the same object, it may not work properly.

Tripwire has no action that calls ListProductOwners. This event happens when another UdonSharp script makes the call with this trigger as the receiver.

Official docs: [Udon Documentation (VRChat)](https://creators.vrchat.com/economy/sdk/udon-documentation/)
