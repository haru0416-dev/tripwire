Happens when someone uses Store.SendProductEvent. It reaches the named UdonBehaviour on every screen in the instance. **That player** is the player who used the product.

Before it is sent, both the sender's client and VRChat's servers check that the sender has purchased the product.

Tripwire has no action that calls SendProductEvent. This event happens when another UdonSharp script sends it with this trigger as the target. **Whose** on the card starts as **Me**.

Official docs: [Udon Documentation (VRChat)](https://creators.vrchat.com/economy/sdk/udon-documentation/)
