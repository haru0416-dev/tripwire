Animator Controller の Int パラメータに整数を入れます。遷移の条件で値を比べれば、1 つのパラメータで 3 つ以上の状態を分けられて、Bool をいくつも用意するより簡単です。名前は、Animator のパラメータ名とそろえてください。

「値」には整数の変数も選べます。同期する整数の変数を「変数が変わったとき」で受けて「値」に入れれば、照明の 3 段階のような状態を全員でそろえられます（[全員で同じ状態にする](/guide/sync)）。このアクションだけでは、変わるのは動いた画面の Animator だけです。

公式の説明: [Animator.SetInteger（Unity）](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Animator.SetInteger.html)
