# Distributing and selling

Gimmicks made with Tripwire can go to people who don't have Tripwire. "Export for Distribution" makes a .unitypackage that needs only UdonSharp (the VRChat SDK).

## Why a separate export

The cards live in the Tripwire component. Passed on with Unity's own Export Package, that component becomes a "Missing Script" for anyone without Tripwire. The gimmick still works, but Unity won't save a prefab with a missing script, so the person can't adjust its color or layout.

"Export for Distribution" takes the Tripwire components out and puts in the generated scripts. Your own prefabs and scenes don't change.

## How to export

1. Select the prefabs, scenes or folders to hand over in the Project window
2. Right click > Tripwire > Export for Distribution…, or Tools > Tripwire > Export for Distribution…
3. Check what goes in, what loses Tripwire, and which packages the person needs, then press Export…

The export first checks that the triggers are applied. A trigger that isn't applied, or a scene with unsaved changes, stops it with what to do. Scenes that aren't open can't be checked; open them once to be sure.

## For the person receiving it

- The gimmicks work as they are. They need the VRChat SDK (Worlds)
- Colors, sizes and layout can be changed like any prefab
- The cards can't be edited

## Handing over the cards too

With "Keep the cards" on in the export window, the Tripwire components stay. The person receiving it needs Tripwire from VCC, and can read and edit the cards. You can hand over both versions for people who use Tripwire; ask them to import only one of the two into a project.

TextMesh Pro's essential resources (TMP Essential Resources) aren't included either. When the receiving project doesn't have them, Unity offers to import them.

## License

Tripwire is under the MIT License. Worlds and gimmicks made with it can be distributed freely, sold included.
