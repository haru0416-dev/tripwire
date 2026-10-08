# Install

## Requirements

- Unity 2022.3
- VRChat Worlds SDK 3.10.5 or later

## With VCC

<div class="steps">

1. Open [vpm.haru0416.dev](https://vpm.haru0416.dev) and press **Add to VCC**. If the button does nothing, add `https://vpm.haru0416.dev/index.json` in VCC under <span class="menu">Settings → Packages → Add Repository</span>
2. In VCC, open your world project with <span class="menu">Manage Project</span>
3. Press + next to Tripwire. Udon Bridge, which Tripwire uses, comes with it

</div>

ALCOM works the same way.

::: tip Check that it is in
Select an object in Unity: <span class="menu">Add Component → Tripwire → Tripwire Trigger</span> should be there.
:::

## Set up the scene

To try things in play mode, the scene needs a VRCWorld (VRChat's scene descriptor). Without one, the Tripwire Trigger Inspector shows an **Add VRCWorld** button. In play mode, the VRChat SDK's ClientSim spawns your player, so you can click things and walk into areas.

## Import the sample

A sample scene holds the recipes of this manual. In <span class="menu">Window → Package Manager</span>, select Tripwire and press <span class="menu">Import</span> next to Recipes under Samples. It goes to `Assets/Samples/Tripwire`.

## Update and remove

To update, pick the new version in VCC. Triggers saved by older versions keep working. A trigger saved by a newer version can't be edited with an older Tripwire (its Inspector says so).

Before removing Tripwire, apply every trigger. Applied gimmicks run on the generated UdonSharp alone and keep working without Tripwire (the Tripwire Trigger components become Missing Scripts: delete them).
