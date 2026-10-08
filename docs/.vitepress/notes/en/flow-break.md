Leaves the innermost loop (Repeat, For Each, While) and carries on after it. Outside a loop it is an error.

Actions after Break in the same list can never run, and Tripwire warns about them. To leave only when something holds, put Break in the Then of an If.
