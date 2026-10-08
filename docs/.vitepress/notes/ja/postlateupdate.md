毎フレームの終わり近く、IK の計算が終わった後に起きます。ここでプレイヤーのボーンの位置を読むと、1 フレーム遅れずに最新の位置が取れます。

ボーンの位置は、「Udon の機能を呼ぶ」で VRCPlayerApi の GetBonePosition を読みます。読んだ位置を変数に入れて「位置を変える」に渡すと、物を手や頭について行かせられます。

公式の説明: [Events（UdonSharp）](https://udonsharp.docs.vrchat.com/events/)
