Animator Controller に作った Trigger パラメータを、名前で送ります。名前は、Animator ウィンドウの Parameters にある名前とそろえてください。

Trigger は、遷移に使われると Animator が自動でオフに戻すパラメータです。扉を一度開ける、ジャンプの動きを一回流す、といった 1 回きりの動きに向いています。

送るのは、このアクションが動いた画面の Animator だけです。1 回きりの合図なので、あとから来た人には届きません。開いている・閉じているのように状態を全員でそろえるなら、同期する変数と「Animator の Bool を変える」を組み合わせます（[全員で同じ状態にする](/guide/sync)）。

公式の説明: [Animator.SetTrigger（Unity）](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Animator.SetTrigger.html)、[Animation Parameters（Unity）](https://docs.unity3d.com/2022.3/Documentation/Manual/AnimationParameters.html)
