ワールドに置いた VRC PhysBone を、プレイヤーがつかんだときに起きます。イベントは PhysBone と同じオブジェクトの UdonBehaviour にしか届きません。Tripwire Trigger は PhysBone と同じオブジェクトに付けてください。つかめるのは、PhysBone の Allow Grabbing がオンのときです。

今つかまれているかは、PhysBone の IsGrabbed で分かります。手を離させるには、「Udon の機能を呼ぶ（Udon API）」で ReleaseGrabs を呼びます。これは呼んだ画面の人の手しか離させません。全員の手を離させるときは、そのアクションを置いたカードの詳細設定を「全員（All）」にします。

公式の説明: [PhysBones（VRChat）](https://creators.vrchat.com/common-components/physbones/)
