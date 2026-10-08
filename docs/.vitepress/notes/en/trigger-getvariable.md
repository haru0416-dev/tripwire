Puts the current value of a variable of **Trigger** into this trigger's **Into** variable. It reads the value on the screen where the action runs; for a synced variable, that's the last value received.

Reading copies the value once: later changes to the other variable don't follow. To get every change, have the other trigger's On Variable Changed call a Custom event of this trigger.

**Into** must be a variable that can hold the value read. The other trigger's temporary variables can't be read, since only that trigger can use them.
