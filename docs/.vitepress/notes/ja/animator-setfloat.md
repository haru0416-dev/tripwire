Animator Controller の Float パラメータに小数を入れます。遷移の条件のほか、Blend Tree の混ぜ具合を動かすのにも使えます。名前は、Animator のパラメータ名とそろえてください。

このアクションでは、パラメータはすぐにその値になります。「値」には数の変数やイベントの値も選べるので、「スライダーが動いたとき」の値をそのまま入れると、スライダーでアニメーションを動かせます。

変わるのは、このアクションが動いた画面の Animator だけです。全員でそろえるときは、同期する数の変数を「変数が変わったとき」で受けて、その中でこのアクションを動かします（[全員で同じ状態にする](/guide/sync)）。

公式の説明: [Animator.SetFloat（Unity）](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Animator.SetFloat.html)、[Animation Parameters（Unity）](https://docs.unity3d.com/2022.3/Documentation/Manual/AnimationParameters.html)
