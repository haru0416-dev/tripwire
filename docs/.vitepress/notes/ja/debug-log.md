「メッセージ」に `{点数}` のように変数の名前を書くと、その変数の今の値に置き換わります。書き方は「テキストを変える」と同じです。イベントが動いたか、変数がいくつになっているかを確かめるのに使います。

Unity の Play 中は、Console ウィンドウに出ます。Tripwire の動きを確かめるだけなら、カードの表示や「実行の記録」でも足ります（[Play で試す](/guide/testing)）。

VRChat の中では、出力ログ（output log）に書かれます。Windows での場所は `C:\Users\ユーザー名\AppData\LocalLow\VRChat\VRChat` です。起動するたびに、新しいファイルが作られます。起動オプションに `--enable-debug-gui` を付けると、右 Shift と ` と 3 を同時に押して、ログを VRChat の中で見られます。

公式の説明: [Debug.Log（Unity）](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Debug.Log.html)、[Debugging Udon Projects（VRChat）](https://creators.vrchat.com/worlds/udon/debugging-udon-projects)
