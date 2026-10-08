Happens when you change VRChat's display language. It also happens when you join, so localizing signs can all live in this one card.

Languages are written in RFC 5646 form, like en, ja or zh-CN (as documented for GetCurrentLanguage, which returns the current one). Put **Language code** into a Text variable with **Set Variable**, compare it with ja and so on in an If, and use **Set Text** in each branch.

Official docs: [Event Nodes (VRChat)](https://creators.vrchat.com/worlds/udon/graph/event-nodes/), [Release 3.7.0 (VRChat)](https://creators.vrchat.com/releases/release-3-7-0/), [Player API (VRChat)](https://creators.vrchat.com/worlds/udon/players/)
