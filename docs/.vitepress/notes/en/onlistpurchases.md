Happens on your screen as the answer to a Store.ListPurchases call made there. **The products** lists what the requested player has bought.

Tripwire has no action that calls ListPurchases. This event happens when another UdonSharp script makes the call with this trigger as the receiver. To just learn a joining player's purchases, On Purchases Loaded happens by itself ([On Purchases Loaded](/en/reference/events/onpurchasesloaded)).

**Whose** on the card starts as **Me**: the actions run only when the requested player is you.

Official docs: [Udon Documentation (VRChat)](https://creators.vrchat.com/economy/sdk/udon-documentation/)
