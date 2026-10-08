「移動先」のオブジェクトと同じ位置と向きへ、対象を動かします。どちらもワールド座標でそろえるので、親子関係が違っていても同じ場所に重なります。大きさ（Scale）は変わりません。

移動先には、空のオブジェクトを目印として置くのが手軽です。Scene ビューで目印を動かせば、行き先と向きを決められます。

動くのは、このアクションが動いた画面だけです。ほかの人の画面では元の位置のままです。全員の画面でそろえたいときは、オブジェクトに VRC Object Sync を付けて、位置と向きを同期させます。プレイヤー自身を動かすときは「テレポートする」を使います。

公式の説明: [Transform.SetPositionAndRotation（Unity）](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Transform.SetPositionAndRotation.html)、[VRC Object Sync（VRChat）](https://creators.vrchat.com/worlds/components/vrc_objectsync/)
