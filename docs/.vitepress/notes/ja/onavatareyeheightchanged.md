アバターを変えたときと、アバターのスケールを変えたときに起きます。ほかの人の身長が変わったときも対象です。カードの「誰のとき」で、どの人のときに動かすかを選びます。

「前の目の高さ」は、変わる前の目の高さ（メートル）です。ワールドに入った直後の最初の 1 回は、0 になることがあります。今の高さを知りたいときは、「Udon の機能を呼ぶ（Udon API）」で GetAvatarEyeHeightAsMeters を呼んでください。ほかの人の高さも読めます。

自分がアバターを変えたときは、保存した身長があれば、その高さになったときだけ起きます。ほかの人のときは、新しい高さが届くたびに起きるので、1 回の変更で何度か起きることがあります。[アバターを変えたとき](/reference/events/onavatarchanged) より先に起きることもありますが、高さが届いた順番は入れ替わりません。

プレイヤーが自分で変えられる目の高さは 0.2〜5 m です。スケールを変えても、まわりとの当たり判定の大きさは変わりません。

公式の説明: [Avatar Events（VRChat）](https://creators.vrchat.com/worlds/udon/avatar-events/)、[Player Avatar Scaling（VRChat）](https://creators.vrchat.com/worlds/udon/players/player-avatar-scaling/)
