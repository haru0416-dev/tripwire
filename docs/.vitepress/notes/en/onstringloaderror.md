Happens when loading text with VRCStringDownloader.LoadUrl has failed. **The result**'s Error holds the error message, and ErrorCode holds the HTTP error code.

URLs outside the allowed sites (GitHub Pages, Gist, Pastebin, Disbridge, VRCDN) load only for players who have turned on "Allow Untrusted URLs". Downloads are limited to one every five seconds, and extra requests wait in a queue ([On String Load Success](/en/reference/events/onstringloadsuccess)).

Tripwire has no action that starts a download. This event happens when another UdonSharp script makes the request with this trigger as the receiver.

Official docs: [String Loading (VRChat)](https://creators.vrchat.com/worlds/udon/string-loading/)
