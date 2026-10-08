VRCStringDownloader.LoadUrl で頼んだ文字が、Web から届いたときに起きます。届くのは、頼むときに受け取り先にした UdonBehaviour だけです。ファイルの形式は .txt でも .json でも構いません。

「結果」の Result には、UTF-8 として読んだ文字が入っています。ほかの文字コードのときに使うのが、生のバイト列の ResultBytes です。Tripwire では、「Udon の機能を呼ぶ」で「対象」を「結果」にして Result を読み、「結果を入れる変数」に入れます。

文字を読み込めるのは、5 秒に 1 回までです。それより多く頼んだ分は順番待ちになり、ばらばらの順で読み込まれます。1 つの文字は 100 MB まで、順番待ちは 1000 件までです。

読めるのは GitHub Pages（*.github.io）・Gist（gist.githubusercontent.com）・Pastebin・Disbridge・VRCDN のサイトだけです。ほかのサイトは、見る人が設定で「Allow Untrusted URLs」をオンにしていないと読み込まれません。

Tripwire には、読み込みを始めるアクションがありません。このイベントは、ほかの UdonSharp スクリプトがこのトリガーを受け取り先にして頼んだときに起きます。

公式の説明: [String Loading（VRChat）](https://creators.vrchat.com/worlds/udon/string-loading/)
