Light、Animator、AudioSource、UdonBehaviour など、オン・オフのチェックがあるコンポーネントを切り替えます。オブジェクトを丸ごと消さずに、ライトだけ、アニメーションだけを止めたいときに使います。

オフにしたコンポーネントは、毎フレームの更新（Update）が呼ばれなくなります。Inspector でコンポーネント名の横にあるチェックを、アクションで切り替えるのと同じです。

切り替わるのは、このアクションが動いた画面だけです。全員の画面でそろえたいときは、同期する変数を「変数が変わったとき」で受けて、その中でこのアクションを動かします（[全員で同じ状態にする](/guide/sync)）。

公式の説明: [Behaviour.enabled（Unity）](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Behaviour-enabled.html)
