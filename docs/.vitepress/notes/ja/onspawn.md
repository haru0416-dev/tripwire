VRC Object Pool から取り出されたときのイベントですが、公式の説明では非推奨になっています。取り出されたときに何かをするなら、「オブジェクトが有効になったとき」を使ってください。

VRC Object Pool は、持っているオブジェクトの有効・無効を同期します。取り出す（TryToSpawn）のも戻す（Return）のも、プールのオーナーだけです。戻したオブジェクトは自動で無効になり、あとから来た人の画面でも有効・無効がそろいます。

公式の説明: [Network Components（VRChat）](https://creators.vrchat.com/worlds/udon/networking/network-components/)
