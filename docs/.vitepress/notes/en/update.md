Happens every frame while this trigger is enabled. Late Update runs after every Update of the frame.

For something at a fixed interval, a Timer fits better than every frame ([Timer](/en/reference/events/timer)). This event happens on each player's screen, so avoid changing synced variables here.

Official docs: [MonoBehaviour.Update (Unity)](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.Update.html), [LateUpdate (Unity)](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.LateUpdate.html)
