Happens when a player locks a VRC PhysBone in the shape they grabbed it into. Posing needs Allow Posing on. Put the Tripwire Trigger on the PhysBone's object.

The PhysBone's IsPosed tells whether it is posed right now. **Call Udon API** with ReleasePoses lets it go back to its natural shape, but only on the screen where it runs. To release it for everyone, set **Advanced settings** on that card to **Everyone's screen**.

Official docs: [PhysBones (VRChat)](https://creators.vrchat.com/common-components/physbones/)
