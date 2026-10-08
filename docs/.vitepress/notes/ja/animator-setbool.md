Animator Controller の Bool パラメータを、オンかオフにします。遷移の条件に Bool を使えば、オンで開く、オフで閉じる、のように状態で動きを分けられます。名前は、Animator のパラメータ名とそろえてください。

「値」には、オン/オフの変数も選べます。同期する変数を「変数が変わったとき」で受けて、その変数をそのまま「値」に入れれば、扉や照明の状態が全員の画面でそろいます。あとから来た人も、変数を受け取ったときに同じ状態になります（[全員で同じ状態にする](/guide/sync)）。

オブジェクトを非表示にすると、Animator の今のステートはデフォルトでは消えます。残したいときは、Animator の Keep Animator State On Disable をオンにします。

公式の説明: [Animator.SetBool（Unity）](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Animator.SetBool.html)、[Animator.keepAnimatorStateOnDisable（Unity）](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Animator-keepAnimatorStateOnDisable.html)
