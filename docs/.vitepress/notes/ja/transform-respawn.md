「対象」に入れたオブジェクトを、ワールドが始まったときの位置と向きに戻します。散らかったピックアップを片付けるリセットボタン向けのアクションです。戻す位置は、このトリガーが動き出したとき（ふつうはワールドを読み込んだとき）に覚えておきます。そのため、対象にはオブジェクトをドラッグで入れてください。

VRC Object Sync が付いたオブジェクトは、オーナーになってから Object Sync の Respawn で戻すので、全員の画面で元の位置に戻ります。付いていないオブジェクトは、このアクションが動いた画面でだけ戻り、Rigidbody の速さと回転も止まります。

自分が手に持っているピックアップは、先に手から離します。ほかの人が持っているピックアップは、その人が持ったままです。

カードの詳細設定は「自分だけ（Local）」のままにしてください。「全員（All）」にすると、Object Sync の付いた物を全員が同時にオーナーになって戻そうとします。

公式の説明: [VRC Object Sync（VRChat）](https://creators.vrchat.com/worlds/components/vrc_objectsync/)、[VRC Pickup（VRChat）](https://creators.vrchat.com/worlds/components/vrc_pickup/)
