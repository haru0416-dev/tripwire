Happens when a player's purchase has been loaded and confirmed: when you join the instance (yours and everyone else's), when another player joins, and when anyone in the instance buys one of the world's products. **Bought just now** is on for a purchase made right then, and off when it was loaded on joining.

**Quantity** is how many were bought: 1 to 99 for Instant listings with quantity purchases enabled, and always 1 for other listings. With the old On Purchase Confirmed in the same trigger, the same purchase may be detected twice.

A world only gets events for products it uses: reference each UdonProduct from some UdonBehaviour at least once before uploading. Disabled objects and UdonBehaviours don't run most store events.

To test, ClientSim with the UdonProducts Manager can purchase and expire products. In Build & Test, listings are free and purchases expire after 60 seconds. **Whose** on the card starts as **Me**.

Official docs: [Udon Documentation (VRChat)](https://creators.vrchat.com/economy/sdk/udon-documentation/), [Testing Udon Products (VRChat)](https://creators.vrchat.com/economy/sdk/testing/)
