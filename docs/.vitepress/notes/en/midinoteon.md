Happens when a MIDI Note On arrives: a key or pad pressed on a connected MIDI device, or a note from a VRC Midi Player playing back MIDI data.

To receive a device, add a VRC Midi Listener anywhere in the scene. Turn on the events you want under Active Events (all are off at first), and put the trigger's object in Behaviour. Tripwire doesn't set this up for you. For playback, add the trigger's object to the VRC Midi Player's Target Behaviours.

**Channel** is 0-15, the note number is 0-127, and **Velocity** (how fast the key was hit) is 0-127. Some devices don't send the full note range or any velocity.

VRChat opens the first MIDI device it finds on the PC. To pick another, add `--midi=part-of-its-name` to the launch options.

Official docs: [Midi in Udon (VRChat)](https://creators.vrchat.com/worlds/udon/midi/), [Realtime Midi (VRChat)](https://creators.vrchat.com/worlds/udon/midi/realtime-midi/), [Midi Playback (VRChat)](https://creators.vrchat.com/worlds/udon/midi/midi-playback/)
