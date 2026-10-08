Contact Sender が VRC Contact Receiver から離れたときに起きます。[コンタクトが触れたとき](/reference/events/oncontactenter) と同じく、Tripwire Trigger は Receiver と同じオブジェクトに付けます。

「コンタクトの情報」には、離れた Sender（contactSender）と、共通するタグ（matchingTags）が入っています。触れたときと違い、速さと位置は入っていません。contactSender の中身を使う前に、isValid がオンかを確かめてください。

公式の説明: [Contacts（VRChat）](https://creators.vrchat.com/common-components/contacts/)
