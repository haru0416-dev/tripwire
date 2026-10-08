Loads the **URL** and plays it. In VRChat a URL (VRCUrl) can't be made at runtime; it is fixed in the editor. So **URL** takes a URL typed in the Inspector or a URL variable. You can't build a URL by joining text.

Each player can load a new URL at most once every 5 seconds. The limit is shared by every video player in the world and counts the URLs set in the players from the start. With two video players, a player who joins later must load two URLs, and loading both at once fails.

It plays only on the screen where the action runs: VRChat's video players don't sync by themselves. With the card's Advanced setting on **Everyone's screen**, everyone present plays it, but players who join later don't get it. To keep them in step too, use a player with its own sync, such as ProTV ([Video players](/en/guide/video-players), [Theater](/en/recipes/theater)).

Videos from sites on VRChat's allowlist, such as YouTube, Twitch and Vimeo, play as they are. Other sites need the viewer to turn on Allow Untrusted URLs. In Public and Group Public instances, a site outside the allowlist plays only if its domain is listed in the world's Video Player Allowed Domains. On Android, video from a site without HTTPS doesn't play.

In Unity's Play mode, VRC Unity Video Player plays URLs that point straight at an mp4 or webm file. Services like YouTube only play inside VRChat. VRC AVPro Video Player doesn't play in the editor, so test it with Build & Test. Live streams need AVPro.

Official docs: [Video Players (VRChat)](https://creators.vrchat.com/worlds/udon/video-players/), [Video Player Allowlist (VRChat)](https://creators.vrchat.com/worlds/udon/video-players/www-whitelist), [VRCUrl (UdonSharp)](https://udonsharp.docs.vrchat.com/vrchat-api)
