# Teleport

A button that moves you to a set place.

## You need

- An object to be the button (with a collider)
- An empty object as the destination (with the position and facing you want)

## Cards

<TriggerCard event="Interact"
  :actions="[{ id: 'Player.Teleport', rows: [{ p: 0, value: 'Teleport Point', kind: 'object' }] }]" />

Only the player who pressed moves.

## Teleport on entering an area

Instead of a button, move whoever walks into an area.

<TriggerCard event="OnPlayerTriggerEnter" :rows="[{ label: { ja: '誰が入ったら', en: 'Who enters' }, value: { ja: '自分', en: 'Me' } }]"
  :actions="[{ id: 'Player.Teleport', rows: [{ p: 0, value: 'Teleport Point', kind: 'object' }] }]" />

The area needs a collider with Is Trigger on; without one the card shows **Add an area**.

Set **Who enters** to **Me**. With **Anyone**, when someone else walks in the teleport also runs on your screen and moves you.
