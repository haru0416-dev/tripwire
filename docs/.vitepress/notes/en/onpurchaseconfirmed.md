The official docs mark this event deprecated, and it doesn't support quantity purchases. For new work, use On Purchase Confirmed (Quantity) ([OnPurchaseConfirmedMultiple](/en/reference/events/onpurchaseconfirmedmultiple)). With both in the same trigger, the same purchase may be detected twice.

It happens when a purchase has been loaded and confirmed. Purchases are loaded when you join the instance (yours and everyone else's), when another player joins, and when anyone in the instance buys one of the world's products. **Bought just now** is on for a purchase made right then, and off when it was loaded on joining.

**Whose** on the card starts as **Me**, so the actions run only for your own purchases.

Official docs: [Udon Documentation (VRChat)](https://creators.vrchat.com/economy/sdk/udon-documentation/)
