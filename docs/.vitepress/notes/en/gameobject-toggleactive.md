Each target flips: shown becomes hidden and hidden becomes shown. With one shown and one hidden, it swaps the two.

It flips only on the screen where the action runs. To keep everyone in step, use a synced variable with Set Active.

What flips is each object's own checkbox (activeSelf). On a child hidden by its parent, only the checkbox flips; the child shows once all its parents are shown.

Official docs: [GameObject.SetActive (Unity)](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/GameObject.SetActive.html)
