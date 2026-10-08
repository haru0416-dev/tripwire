Happens when someone sits in the VRC Station. VRChat's docs describe it as happening on your screen when you sit in this object's station, so setting **Whose** to **Me** is the safe choice.

A VRC Station alone doesn't let players sit by clicking. VRChat's docs set up a chair with a VRC Station, a collider, and a program that seats the player (the SDK's StationGraph, which calls UseAttachedStation on Interact). The SDK's VRCChair3 prefab shows the setup.

Station Enter Player Location sets where the seated player goes, and Station Exit Player Location where they go when they get up. With Disable Station Exit on, players can't leave by the usual means.

To show other players who is seated, change a synced variable and react with On Variable Changed ([The same state for everyone](/en/guide/sync)).

Official docs: [VRC Station (VRChat)](https://creators.vrchat.com/worlds/components/vrc_station/), [Event Nodes (VRChat)](https://creators.vrchat.com/worlds/udon/graph/event-nodes/)
