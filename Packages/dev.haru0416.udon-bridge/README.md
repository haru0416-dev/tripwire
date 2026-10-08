# Udon Bridge

Editor helpers over UdonSharp and Udon internals, kept in one package so that an SDK update that moves one of them is
fixed in one place. Tripwire uses it; VCC installs it with Tripwire.

- `UdonNodes`: Udon's node definitions (what Udon exposes, types a variable can hold, types with `==`, events)
- `UdonNames`: UdonSharp's names for members (inherited members named for the type they are called on, fields through
  `get_` / `set_`)
- `UdonSharpPrograms`: program assets, and moving a behaviour to another program in place (references and the enabled
  state survive)
- `HotReload`: in play mode, recompile U# only and put the new programs into the running behaviours
- `UdonSharpSettings`, `UdonSharpCheck` (assembly `UdonBridge.Compiler`): UdonSharp's compile settings, and its bind /
  emit / assembler run on a Roslyn compilation in memory

The internals are reached by reflection; each part reports `Available = false` instead of failing when an SDK no longer
has what it needs. Checked with VRChat Worlds SDK 3.10.5.

MIT License (LICENSE.md).
