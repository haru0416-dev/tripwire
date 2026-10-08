このトリガーが有効なあいだ、毎フレーム、すべての Update が終わった後に起きます。Update の中で動いたものを追いかける動き（物を追うカメラなど）は、こちらに置くのが Unity の勧めです。

プレイヤーのボーンに合わせたいときは、VRChat が IK を計算した後に起きる「毎フレーム（プレイヤーが動いた後）」を使います（[PostLateUpdate](/reference/events/postlateupdate)）。

公式の説明: [MonoBehaviour.LateUpdate（Unity）](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.LateUpdate.html)
