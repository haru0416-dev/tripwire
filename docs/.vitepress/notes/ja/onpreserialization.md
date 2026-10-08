同期する変数を送り出す直前に起きます。公式の説明では、ほかの人に届けたい値をここで入れるとよい、とされています。

「変えたときに送る」では、値を続けて変えても、VRChat は前の送信から十分な時間がたつまで待ちます。待ったあとは、このイベントが起きてから送信し、最後に「同期データを送った後」が起きます。一度に送る量が多いほど、次に送れるまでの間隔は長くなるしくみです。

Tripwire は同期する変数を変えたときに自動で送るので、ふつうはこのイベントを使わなくて構いません。このトリガーに同期する変数がないと起きません。

公式の説明: [Network Components（VRChat）](https://creators.vrchat.com/worlds/udon/networking/network-components/)、[Networking Specs & Tricks（VRChat）](https://creators.vrchat.com/worlds/udon/networking/network-details/)
