# Theater

Each button plays its own video, and the lights dim when playback starts. Examples for VRChat's video player and for ProTV.

## With VRChat's video player

### Play buttons

Add a Tripwire Trigger to each button.

<TriggerCard event="Interact"
  :actions="[{ id: 'Video.PlayUrl', rows: [{ p: 0, value: 'Screen', kind: 'object' }, { p: 1, value: 'https://…', kind: 'text' }] }]" />

::: warning Watching together
VRChat's video players don't sync playback by themselves: as above, the video plays only for whoever pressed. To play it for everyone, set the card to **Everyone's screen** in Advanced settings; players who join later still won't get it. For that, a player with its own sync, such as ProTV, is the reliable choice.
:::

### Dimming the lights

Add a Tripwire Trigger to the video player's own object (Screen): video events only reach triggers on that object.

<TriggerCard event="OnVideoStart"
  :actions="[{ id: 'GameObject.SetActive', rows: [{ p: 0, value: 'Room Lights', kind: 'object' }, { p: 1, value: { ja: 'オフ', en: 'Off' } }] }]" />

<TriggerCard event="OnVideoEnd"
  :actions="[{ id: 'GameObject.SetActive', rows: [{ p: 0, value: 'Room Lights', kind: 'object' }, { p: 1, value: { ja: 'オン', en: 'On' } }] }]" />

## With ProTV

Give each play button **Use another script** with the object holding TVManager as its target, and pick **Play URL** in **Use**.

The lights can listen from a trigger on any object.

<TriggerCard event="ScriptNotified" :rows="[{ label: { ja: '通知してくるもの', en: 'Notified by' }, value: 'TVManager', kind: 'object' }, { label: { ja: '何が起きたとき', en: 'What happened' }, value: { ja: '再生されたとき', en: 'Started playing' } }]"
  :actions="[{ id: 'GameObject.SetActive', rows: [{ p: 0, value: 'Room Lights', kind: 'object' }, { p: 1, value: { ja: 'オフ', en: 'Off' } }] }]" />

::: info Lights in step for everyone
ProTV and similar players sync playback for everyone. Each screen dims its lights when it gets the notification, so the lights agree without a synced variable.
:::
