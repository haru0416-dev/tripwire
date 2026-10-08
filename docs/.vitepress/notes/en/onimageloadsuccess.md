Happens when an image requested with VRCImageDownloader's DownloadImage has arrived. In UdonSharp, no event arrives unless the request names a receiving UdonBehaviour. If the request passed a Material, the image is applied to its main texture automatically.

Images can be at most 2048 × 2048 pixels; larger ones fail. One image can be downloaded every five seconds for the whole scene, and extra requests are queued. The URL must point directly at the image file: redirects fail.

Only certain sites are allowed, such as Imgur (i.imgur.com), GitHub Pages, Dropbox and VRChat (assets.vrchat.com). Other sites load only for players who have turned on "Allow Untrusted URLs".

Images take a lot of memory. When replacing an image with a new one, free the old one with Dispose. Downloading new images without freeing old ones can make visitors run out of memory and crash.

Tripwire has no action that starts a download. This event happens when another UdonSharp script makes the request with this trigger as the receiver.

Official docs: [Image Loading (VRChat)](https://creators.vrchat.com/worlds/udon/image-loading/)
