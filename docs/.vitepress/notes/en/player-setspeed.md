Changes the walk, run and strafe speeds and jump strength of the player on whose screen it runs. The defaults for walk, run and strafe (2, 4, 2) are VRChat's own, and jump 3 is what the VRCWorld prefab sets; jump 0 means no jumping.

To change speeds for the whole world, put it in **Start**: it then runs on each joining player's screen.

VRChat suggests working ranges of about 0 to 5 for walk and strafe, and about 0 to 10 for run and jump. Keep run above walk. Running doesn't speed up strafing, so strafe is best set to the same as walk.

Only your own speeds change; other players' speeds can't be set.

Official docs: [Player Forces (VRChat)](https://creators.vrchat.com/worlds/udon/players/player-forces/)
