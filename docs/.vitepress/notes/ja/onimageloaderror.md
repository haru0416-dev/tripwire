VRCImageDownloader での画像の読み込みに失敗したときに起きます。「結果」の Error に失敗の理由が、ErrorMessage にその説明の文字が入っています。

Error の値は InvalidURL（URL が正しくない）、AccessDenied（アクセスを拒否された）、InvalidImage（画像として読めない）のどれかです。ほかに DownloadError（通信のエラー）と Unknown（不明なエラー）があります。

2048 × 2048 ピクセルより大きい画像や、リダイレクトする URL もエラーになります。許可されたサイト以外は、見る人が「Allow Untrusted URLs」をオンにしていないと読み込まれません（[画像の読み込みが終わったとき](/reference/events/onimageloadsuccess)）。

Tripwire には、読み込みを始めるアクションがありません。このイベントは、ほかの UdonSharp スクリプトがこのトリガーを受け取り先にして頼んだときに起きます。

公式の説明: [Image Loading（VRChat）](https://creators.vrchat.com/worlds/udon/image-loading/)
