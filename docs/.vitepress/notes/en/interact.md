The card's **Hover text** is what players see when they point at the object. Left empty, VRChat's usual "Use" shows.

Actions after a click normally run only on the screen of whoever clicked. To show the result to everyone, change a synced variable and react with On Variable Changed ([A switch everyone shares](/en/recipes/shared-switch)).

Setting **Everyone's screen** in the advanced settings only reaches players who are there at that moment: VRChat doesn't repeat network events for players who join later.

Only objects with a collider can be clicked. Most VRChat layers block the pointer, so another collider in front can make the object unclickable. The UiMenu, UI, PlayerLocal and MirrorReflection layers let it through. User layers block it too, unless they are in the Interact Passthrough mask.

For a button on a Canvas, [UI Button Pressed](/en/reference/events/uibuttonclick) fits better than this event.

Official docs: [Event Nodes (VRChat)](https://creators.vrchat.com/worlds/udon/graph/event-nodes/), [Layers (VRChat)](https://creators.vrchat.com/worlds/layers/), [Custom Network Events (VRChat)](https://creators.vrchat.com/worlds/udon/networking/events)
