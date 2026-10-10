TextMeshPro（3D のもの）と、Canvas の中の TextMeshPro（UGUI）のどちらにも使えます。

文字の中に `{スコア}` のように変数の名前を書くと、その変数の今の値に置き換わります。`{` や `}` を文字として出したいときは、2 つ重ねて書きます。`{プレイヤー数}`、`{自分の名前}`、`{時刻}` は、変数を作らなくても使えます（[文字に値を入れる](/guide/variables#文字に値を入れる)）。欄は複数行なので、改行もそのまま書けます。

書き換わるのは、このアクションが動いた画面だけです。全員に同じ文字を見せるときは、同期する変数を「変数が変わったとき」で受けて、その中でこのアクションを動かします（[スコアボード](/recipes/scoreboard)）。

対象の TextMeshPro で Rich Text がオンなら、文字の中に書いたタグも使えます。タグを文字のまま見せたいときは、TextMeshPro の Inspector で Rich Text をオフにします。

公式の説明: [TMP_Text（TextMeshPro）](https://docs.unity3d.com/Packages/com.unity.textmeshpro@3.0/api/TMPro.TMP_Text.html)
