# Video players

Tripwire works with VRChat's video players and with ProTV, VizVid, USharpVideo and VideoTXL.

## VRChat's video players

VRC Unity Video Player and VRC AVPro Video Player are controlled with the actions in **Video player**: play a URL, pause, stop, seek, loop.

<TriggerCard event="Interact"
  :actions="[{ id: 'Video.PlayUrl', rows: [{ p: 0, value: 'Screen', kind: 'object' }, { p: 1, value: 'https://…', kind: 'text' }] }]" />

::: warning One URL every 5 seconds
VRChat loads a new URL at most once every 5 seconds.
:::

Playback events (started, ended...) are the events in **Video player**. They only reach a trigger on the same object as the video player.

## ProTV, VizVid, USharpVideo, VideoTXL

These send notifications from their scripts.

<div class="steps">

1. For "when", pick **Notified By Another Script**
2. Put the player's object in **Notified by** (for ProTV, the one with TVManager)
3. In **What happened**, pick **Started playing**, **Media ended** and so on

</div>

<TriggerCard event="ScriptNotified" :rows="[{ label: { ja: '通知してくるもの', en: 'Notified by' }, value: 'TVManager', kind: 'object' }, { label: { ja: '何が起きたとき', en: 'What happened' }, value: { ja: '再生されたとき', en: 'Started playing' } }]"
  :actions="[{ id: 'GameObject.SetActive', rows: [{ p: 0, value: 'Room Lights', kind: 'object' }, { p: 1, value: { ja: 'オフ', en: 'Off' } }] }]" />

To control them, use **Use another script** with the player's object as the target and pick **Play URL** or another operation in **Use**.

::: info Not supported yet
YamaPlayer and QvPen need their own base classes to receive notifications, so they aren't supported yet. iwaSync3 isn't listed because the source of its current version isn't public.
:::
