同期する変数を送ろうとした直後に起きます。「結果」には、送れたかどうかの success と、送ったバイト数の byteCount が入っています。

1 回に送れる量には上限があり、「変えたときに送る」でおよそ 280,496 バイト、「常に送り続ける（なめらか）」でおよそ 200 バイトです。公式の型ごとの大きさは目安で、実際に送る量はそれより多くなることがあります。正確な量は byteCount で分かります。

このトリガーに同期する変数がないと起きません。

公式の説明: [Network Components（VRChat）](https://creators.vrchat.com/worlds/udon/networking/network-components/)、[Networking Specs & Tricks（VRChat）](https://creators.vrchat.com/worlds/udon/networking/network-details/)
