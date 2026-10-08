Works with both 3D TextMeshPro and TextMeshPro on a Canvas (UGUI).

A variable's name in braces, like `{score}`, becomes the variable's current value. To show `{` or `}` themselves, double them.

The text changes only on the screen where the action runs. To show everyone the same text, react to a synced variable with On Variable Changed and set the text there ([Scoreboard](/en/recipes/scoreboard)).

If Rich Text is on for the target TextMeshPro, tags in the text work too. To show tags as plain text, turn Rich Text off in the TextMeshPro Inspector.

Official docs: [TMP_Text (TextMeshPro)](https://docs.unity3d.com/Packages/com.unity.textmeshpro@3.0/api/TMPro.TMP_Text.html)
