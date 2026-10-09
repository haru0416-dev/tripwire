# Changelog

## 0.1.1

- The Inspector draws big triggers about ten times faster (72 blocks: 270 ms → 25 ms a pass): the loop check listed each block's links once instead of scanning all of them at every step
- Call Udon API: members with `out` parameters (Physics.Raycast, int.TryParse, PlayerData.TryGetInt...), each received by a variable picked in the Inspector: 132 more members. Members with `ref` parameters stay out: in Udon their values don't come back
- Call Udon API: members that report back to a behaviour (VRCStringDownloader.LoadUrl, VRCImageDownloader.DownloadImage, VRCTween callbacks, SendCustomNetworkEvent with values, Store.ListPurchases...), with this trigger as the receiver by default
- Call Udon API: setters and void methods of structs on a variable (a position's y, a color's alpha, Normalize, VRCTweenHandle.Kill), stored back into the variable
- Call Udon API: members returning an enum Udon can't keep in a variable (VRCPickup.currentHand...) store its number; 14738 members in all (13489 before)
- Calculate: A + − × ÷ % B into a number, position, color or text variable
- Get Component: a component of an object, or of its children or parents, into an object variable of that type
- Checks for combinations that compiled but went wrong: a call writing a synced variable from a broadcast event, when sync arrives or in a synced variable's change block (warned like Set Variable); network events with values sent to a trigger (an error: Custom events take no values); a call reporting back to a trigger without the event it reports to, or naming a Custom event or variable the trigger doesn't have; VRCTween's variableName given a trigger variable (now its Udon name); Get Component many times a second
- A variable written by name through VRCTween's variableName counts as used (Remove Unused Variables keeps it) and follows a rename
- An enum constant its enum doesn't have, or a synced variable of a type Udon can't sync, is an error in the trigger instead of a compile error that stopped every script

## 0.1.0

First public version.

- Inspector event → action lists compiled to UdonSharp: 30 events, 37 actions, variables (synced too), conditions, delays
- Any Udon API member and members of other UdonSharp scripts
- UI events, VRChat video player events and actions, notifications from other scripts
- Presets for ProTV, VizVid, USharpVideo and VideoTXL
- Values as text and {name} placeholders in Set Text and Log
- During Play, each card shows when it last ran, or which condition stopped it with the values; the Event History window lists every run; variable values can be changed in the Inspector to try things
- Export for Distribution: a .unitypackage of prefabs and scenes without the Tripwire component, which works with UdonSharp alone
- Japanese and English editor UI, menus included
