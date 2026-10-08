「ステート名」のステートへ、遷移の条件を待たずに切り替えて再生します。名前は、Animator ウィンドウのステート名と同じものです。Unity の説明では、`Base Layer.Open` のようにレイヤー名を付けた書き方が勧められています。

このアクションはレイヤーを指定しないので、その名前のステートが最初に見つかったレイヤーで再生します。いくつかのレイヤーに同じ名前のステートがあるときは、レイヤー名を付けて区別してください。

切り替わるのは、このアクションが動いた画面だけです。また、オブジェクトを非表示にすると、Animator の今のステートはデフォルトでは消えます。全員の画面やあとから来た人にそろえたい状態は、同期する変数と「Animator の Bool を変える」で持つほうが確実です（[全員で同じ状態にする](/guide/sync)）。

公式の説明: [Animator.Play（Unity）](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Animator.Play.html)、[Animator.keepAnimatorStateOnDisable（Unity）](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Animator-keepAnimatorStateOnDisable.html)
