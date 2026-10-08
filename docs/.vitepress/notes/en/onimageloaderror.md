Happens when loading an image with VRCImageDownloader has failed. **The result**'s Error tells why, and ErrorMessage holds a description.

Error is InvalidURL (the URL is invalid), AccessDenied (access was denied), InvalidImage (what arrived isn't a valid image), DownloadError (a web request error) or Unknown.

Images larger than 2048 × 2048 pixels and URLs that redirect also fail. Sites outside the allowed list load only for players who have turned on "Allow Untrusted URLs" ([On Image Load Success](/en/reference/events/onimageloadsuccess)).

Tripwire has no action that starts a download. This event happens when another UdonSharp script makes the request with this trigger as the receiver.

Official docs: [Image Loading (VRChat)](https://creators.vrchat.com/worlds/udon/image-loading/)
