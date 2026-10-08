「位置」は、シーン全体の中での x, y, z（ワールド座標）です。決まった値のほか、位置の変数やイベントの値も選べます。対象を何個も入れると、全部が同じ位置へ動き、向きはそのままです。

動くのは、このアクションが動いた画面だけです。ほかの人の画面では元の位置のままで、あとから来た人にも届きません。全員の画面でそろえたいときは、オブジェクトに VRC Object Sync を付けます。位置と向きが、自動で全員に同期されます。

置き場所が決まっているなら、空のオブジェクトを目印に置いて「別のオブジェクトの位置へ動かす」を使うほうが、Scene ビューで場所を確かめやすくなります。

公式の説明: [Transform.position（Unity）](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Transform-position.html)、[VRC Object Sync（VRChat）](https://creators.vrchat.com/worlds/components/vrc_objectsync/)
