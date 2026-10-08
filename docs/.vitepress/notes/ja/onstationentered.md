VRC Station に座ったときに起きます。公式の説明では、自分がこのオブジェクトのステーションに座ったときに、自分の画面で起きるとされています。カードの「誰のとき」は「自分」にしておくと確実です。

VRC Station を付けただけでは、クリックしても座れません。公式の説明では、VRC Station とコライダーに加えて、座らせるプログラムを付けます。SDK の StationGraph がそのプログラムで、Interact で UseAttachedStation を呼びます。SDK の VRCChair3 プレハブが、その組み立ての例です。

座ったときの位置は Station Enter Player Location、降りたときの位置は Station Exit Player Location で決めます。Disable Station Exit をオンにすると、ふつうの操作では降りられなくなります。

座っている人をほかの人にも見せる演出は、同期する変数を変えて「変数が変わったとき」で受けます（[全員で同じ状態にする](/guide/sync)）。

公式の説明: [VRC Station（VRChat）](https://creators.vrchat.com/worlds/components/vrc_station/)、[Event Nodes（VRChat）](https://creators.vrchat.com/worlds/udon/graph/event-nodes/)
