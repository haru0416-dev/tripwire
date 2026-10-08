Happens when a MIDI control change arrives, usually from a knob or slider on a MIDI device. Turn this event on under the VRC Midi Listener's Active Events too ([MIDI Note On](/en/reference/events/midinoteon)). A VRC Midi Player only sends Note On and Note Off.

**Channel** is 0-15, the control number 0-127, and **The event's value** 0-127. Endless knobs may send only 0 and 1 or a similar range meaning "up" and "down"; then keep the total yourself, for example with **Add To Variable**.

Official docs: [Midi in Udon (VRChat)](https://creators.vrchat.com/worlds/udon/midi/), [Midi Playback (VRChat)](https://creators.vrchat.com/worlds/udon/midi/midi-playback/)
