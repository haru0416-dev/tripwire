A variable's name in braces in **Message**, like `{score}`, becomes its current value, as in **Set Text**. Use it to check whether an event ran or what a variable holds.

In Unity's Play mode, messages show in the Console window. To check Tripwire itself, the times on the cards and the Event History are often enough ([Trying it in Play](/en/guide/testing)).

Inside VRChat, messages go to the output log. On Windows it is in `C:\Users\YourName\AppData\LocalLow\VRChat\VRChat`, with a new file each launch. Launch VRChat with `--enable-debug-gui` and press Right Shift, ` and 3 together to read the log in game.

Official docs: [Debug.Log (Unity)](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Debug.Log.html), [Debugging Udon Projects (VRChat)](https://creators.vrchat.com/worlds/udon/debugging-udon-projects)
