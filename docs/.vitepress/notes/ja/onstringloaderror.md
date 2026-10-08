VRCStringDownloader.LoadUrl での文字の読み込みに失敗したときに起きます。「結果」の Error にエラーの説明が、ErrorCode に HTTP のエラーコードが入っています。

許可されたサイト（GitHub Pages・Gist・Pastebin・Disbridge・VRCDN）以外の URL は、見る人が「Allow Untrusted URLs」をオンにしていないと読み込まれません。読み込みは 5 秒に 1 回までで、多く頼んだ分は順番待ちになります（[文字の読み込みが終わったとき](/reference/events/onstringloadsuccess)）。

Tripwire には、読み込みを始めるアクションがありません。このイベントは、ほかの UdonSharp スクリプトがこのトリガーを受け取り先にして頼んだときに起きます。

公式の説明: [String Loading（VRChat）](https://creators.vrchat.com/worlds/udon/string-loading/)
