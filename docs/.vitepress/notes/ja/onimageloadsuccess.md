VRCImageDownloader の DownloadImage で頼んだ画像が届いたときに起きます。UdonSharp では、頼むときに受け取り先の UdonBehaviour を指定しないと、このイベントは届きません。頼むときに Material を渡していれば、画像はそのメインのテクスチャに自動で入ります。

画像は 2048 × 2048 ピクセルまでで、それより大きいとエラーになります。読み込めるのはシーン全体で 5 秒に 1 回までで、それを超えた分は順番待ちになります。URL は画像ファイルを直接指すものに限られ、リダイレクトするとエラーです。

読めるサイトは Imgur（i.imgur.com）・GitHub Pages・Dropbox・VRChat（assets.vrchat.com）などに限られます。ほかのサイトは、見る人が「Allow Untrusted URLs」をオンにしていないと読み込まれません。

画像はメモリを多く使います。新しい画像に入れ替えるときは、古いほうを Dispose で解放してください。解放せずに読み込み続けると、見ている人がメモリ不足で落ちることがあります。

Tripwire には、読み込みを始めるアクションがありません。このイベントは、ほかの UdonSharp スクリプトがこのトリガーを受け取り先にして頼んだときに起きます。

公式の説明: [Image Loading（VRChat）](https://creators.vrchat.com/worlds/udon/image-loading/)
