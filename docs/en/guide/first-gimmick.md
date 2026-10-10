# Your first gimmick

A switch that shows and hides a mirror when clicked. Start with your world's scene open in Unity.

## Add a trigger

<div class="steps">

1. Select the object to be the switch (a Cube, say)
2. Add <span class="menu">Add Component → Tripwire → Tripwire Trigger</span>
3. The Inspector shows "When → What to do". It is empty, so starter buttons are listed

</div>

The starter **Click to show / hide** does the steps below in one go. Here they are one by one, so you can see what it makes.

Other starters include **A button only admins can press**, **Show how many players are here**, **Count down from 10**, **Put objects back where they were** and **Count each player's visits (saved)**. Each works once you drag in the objects or texts.

## Pick "when"

Press **+ Add a "when" (event)**. In the list, pick the **Common** category, then **Interact** (clicking the object).

::: tip It needs a collider
Clicking needs a collider on the object. Without one the card shows an **Add a collider** button that adds one fitted to the object.
:::

## List what to do

Press **+ Add what to do** on the card, and pick **Toggle Active** under **Show / hide**. Drag the mirror from the Hierarchy into **Target**.

<TriggerCard event="Interact"
  :actions="[{ id: 'GameObject.ToggleActive', rows: [{ p: 0, value: 'Mirror', kind: 'object' }] }]" />

## Run it

Press Play: the cards become UdonSharp and run. Click the object in ClientSim to try it.

This happens by itself before Play and builds. **Apply now** at the top of the Inspector does it right away.

::: warning A red frame
A card with something missing gets a red frame and says why inside. **Show them** in the status at the top opens only the cards with problems.
:::

## Next

- [When → What to do](./events-and-actions): how cards work
- [Variables and sync](./variables): a switch everyone shares
