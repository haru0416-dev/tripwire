Happens when a player grabs a VRC PhysBone in the world. The event only reaches UdonBehaviours on the PhysBone's own object, so put the Tripwire Trigger there. Players can grab it only with Allow Grabbing on.

The PhysBone's IsGrabbed tells whether it is grabbed right now. **Call Udon API** with ReleaseGrabs makes the player let go, but only on the screen where it runs. To release everyone's grab, set **Advanced settings** on that card to **Everyone's screen**.

Official docs: [PhysBones (VRChat)](https://creators.vrchat.com/common-components/physbones/)
