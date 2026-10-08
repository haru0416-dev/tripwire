プレイヤーが VRC PhysBone を、つかんだ形のまま固定したときに起きます。ポーズできるのは、PhysBone の Allow Posing がオンのときです。Tripwire Trigger は PhysBone と同じオブジェクトに付けます。

今ポーズされているかは、PhysBone の IsPosed で分かります。「Udon の機能を呼ぶ（Udon API）」で ReleasePoses を呼ぶと、固定が解けて元の形に戻ります。ただし呼んだ画面でしか解けません。全員の画面で解くときは、そのアクションを置いたカードの詳細設定を「全員（All）」にします。

公式の説明: [PhysBones（VRChat）](https://creators.vrchat.com/common-components/physbones/)
