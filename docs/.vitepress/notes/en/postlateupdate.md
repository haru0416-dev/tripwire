Happens near the end of each frame, after IK has been calculated. Bone positions read here are up to date, not a frame behind.

Read a bone's position with **Call Udon API** (VRCPlayerApi's GetBonePosition). Store it in a variable and pass it to **Set Position** to keep an object on a hand or the head.

Official docs: [Events (UdonSharp)](https://udonsharp.docs.vrchat.com/events/)
