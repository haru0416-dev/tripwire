Ends the event without running any of its remaining actions. Usually inside an If, to stop when something isn't right.

Inside a loop it still ends the whole event, not just the loop; use Break to leave only the loop. Actions after Return in the same list never run, and Tripwire warns about them.
