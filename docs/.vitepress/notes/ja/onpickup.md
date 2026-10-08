VRC Pickup の付いたオブジェクトを、自分が手に持ったときに起きます。起きるのは持った人の画面だけです。ほかの人にも結果を見せたいときは、同期する変数を変えて「変数が変わったとき」で受けてください（[全員で同じ状態にする](/guide/sync)）。

ピックアップには、同じオブジェクトに Rigidbody とコライダーが必要です。カードの「VRC Pickup を付ける」は、コライダーがなければコライダーも付けます。Rigidbody は VRC Pickup と一緒に付きます。

持って動かしても、そのままではほかの人の画面ではオブジェクトが動きません。動きを全員にそろえるには、同じオブジェクトに VRC Object Sync を付けます。指したときに出る文字は、VRC Pickup の Interaction Text で決めます。

鍵を拾うと扉が開く例は [鍵の扉](/recipes/locked-door) にあります。

公式の説明: [VRC Pickup（VRChat）](https://creators.vrchat.com/worlds/components/vrc_pickup/)、[Event Nodes（VRChat）](https://creators.vrchat.com/worlds/udon/graph/event-nodes/)
