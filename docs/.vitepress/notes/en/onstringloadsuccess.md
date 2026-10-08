Happens when text requested with VRCStringDownloader.LoadUrl has arrived from the web. Only the UdonBehaviour named as the receiver in the request gets it. The file can be in any format, such as .txt or .json.

**The result**'s Result holds the text decoded as UTF-8. For other encodings, use ResultBytes, the raw bytes. In Tripwire, use **Call Udon API** with **The result** as the target, read Result, and pick a variable in **Store result in**.

One string can be downloaded every five seconds. Requests beyond that are queued and downloaded in a random order. A string can be at most 100 MB, and the queue holds at most 1000 requests.

Only GitHub Pages (*.github.io), Gist (gist.githubusercontent.com), Pastebin, Disbridge and VRCDN are allowed. Other sites load only for players who have turned on "Allow Untrusted URLs" in their settings.

Tripwire has no action that starts a download. This event happens when another UdonSharp script makes the request with this trigger as the receiver.

Official docs: [String Loading (VRChat)](https://creators.vrchat.com/worlds/udon/string-loading/)
