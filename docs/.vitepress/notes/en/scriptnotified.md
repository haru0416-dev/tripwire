Happens when the script in **Notified by** calls this trigger by the chosen name. For ProTV, VizVid, USharpVideo and VideoTXL, picking from **What happened** fills in the name and the registration ([Video players](/en/guide/video-players)).

For other scripts, choose **Set up by hand…** and set **Register with** and **Callback name**. Write the callback name exactly as the other script calls it. If the script takes this trigger in its own Inspector instead, set **Register with** to **Don't register (set it up in the script's Inspector)**.

Tripwire registers once, when the world starts (Start). Several cards registering with the same object in the same way share one registration.

Notifications normally arrive on each player's screen separately. Keep the card's advanced setting at **Only my screen**; anything else can run the actions once per player. Even for names that don't start with "_", calls that come over the network are ignored.

The same callback name can't be used on cards with different senders, because the trigger couldn't tell where a call came from. A Custom event can't share the name either ([Other triggers and scripts](/en/guide/linking)).
