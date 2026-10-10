ライトや音源、Animator、ほかの UdonBehaviour などのコンポーネントを、有効なら無効に、無効なら有効にします。オブジェクト自体は表示されたままです。決まった状態にしたいときは「[コンポーネントのオン・オフ](/reference/actions/behaviour-setenabled)」を使います。

切り替わるのは、このアクションが動いた画面だけです。全員の画面でそろえたいときは、同期する変数を切り替えて、「変数が変わったとき」で「コンポーネントのオン・オフ」に変数を入れてください（[全員で同じ状態にする](/guide/sync)）。

公式の説明: [Behaviour.enabled（Unity）](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Behaviour-enabled.html)
