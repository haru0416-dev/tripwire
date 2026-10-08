Happens when the video player fails to load the video. Use it to tell viewers, for example by showing "Couldn't load the video" on a sign. To branch on **Error kind**, make a VideoError variable in **Detailed** mode, fill it with **Set Variable** and compare it in an If.

Each player may load a new URL only once every 5 seconds, counted across all video players, default URLs included. With two video players, a player who joins later starts both loads at once and they fail; space the loads out.

Videos from sites outside the allowlist only play for viewers who turned on Allow Untrusted URLs in their settings.

Official docs: [Event Nodes (VRChat)](https://creators.vrchat.com/worlds/udon/graph/event-nodes/), [Video Players (VRChat)](https://creators.vrchat.com/worlds/udon/video-players/)
