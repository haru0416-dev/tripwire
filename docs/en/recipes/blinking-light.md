# Blinking light

A light that turns on and off every 3 to 6 seconds at random, until a switch stops it.

## You need

- The light
- A switch to stop it (with a collider)

## Cards

Put the Tripwire Trigger on another object (the switch, say), not on the light: hiding the light would stop the trigger too.

<TriggerCard event="Timer" :name="{ ja: '点滅', en: 'blink' }"
  :rows="[{ label: { ja: '間隔', en: 'Every' }, value: { ja: '3 〜 6 秒（ランダム）', en: '3 – 6 seconds (random)' }, kind: 'text' }, { label: { ja: '実行', en: 'Runs' }, value: { ja: 'くり返す', en: 'Repeatedly' } }]"
  :actions="[{ id: 'GameObject.ToggleActive', rows: [{ p: 0, value: 'Lamp', kind: 'object' }] }]" />

<TriggerCard event="Interact"
  :actions="[{ id: 'Timer.Stop', rows: [{ p: 0, value: { ja: '点滅', en: 'blink' } }] }]" />

Timers run on each player's screen, so the blinking isn't in step between players.

## Variations

- Press again to restart: make the second card an If on a non-synced variable that tracks whether it runs, with Start Timer and Stop Timer
- A steady rhythm: turn **Random** off
