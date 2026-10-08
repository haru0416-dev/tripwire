Changes a variable of **Trigger** just as if that trigger changed it itself. That trigger's On Variable Changed runs, and a synced variable reaches everyone. For a synced variable, the player on whose screen the action runs becomes that trigger's owner.

To call a Custom event with a value, set the variable with this action first, then follow with **Send Event** ([Other triggers and scripts](/en/guide/linking)). A value put into a variable that isn't synced exists only in the trigger on the screen where the action ran.

**Value** must fit the type of that variable. The other trigger's temporary variables can't be picked, since only that trigger can use them.
