Moves the player on whose screen the action runs to the position and facing of **Destination**. It can't move other players.

From an area, set the card's **Who enters** to **Me**: with **Anyone**, someone else walking in moves you too ([Teleport](/en/recipes/teleport)).

Other players see the move as instant. Moving farther than 1 m or turning more than 45 degrees resets the avatar's animation and IK. While seated in a station, the station's settings may prevent the teleport.

Teleporting while a network update is being handled can get the avatar caught on nearby geometry. In On Deserialization, or in On Variable Changed for a synced variable, use the card's **Delay** in Advanced settings to run it a little later.

Official docs: [Player Positions (VRChat)](https://creators.vrchat.com/worlds/udon/players/player-positions/)
