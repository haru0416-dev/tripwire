誰かが Store.SendProductEvent を使ったときに起きます。このイベントは、インスタンスにいる全員の画面で、指定した UdonBehaviour に届きます。「そのプレイヤー」は商品を使った人です。

送る前に、送った本人の画面と VRChat のサーバーが、その人が商品を買っているかを確かめます。買っていない人は送れません。

Tripwire には、SendProductEvent を呼ぶアクションがありません。このイベントは、ほかの UdonSharp スクリプトがこのトリガーを送り先にしたときに起きます。カードの「誰のとき」は最初「自分」です。

公式の説明: [Udon Documentation（VRChat）](https://creators.vrchat.com/economy/sdk/udon-documentation/)
