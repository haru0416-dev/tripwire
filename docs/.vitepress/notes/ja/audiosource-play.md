AudioSource に設定した音を、最初から鳴らします。鳴っている途中でもう一度動かすと、最初から鳴らし直します。鳴らす音（AudioClip）を決めておく場所は、AudioSource の Inspector です。

同じ音を重ねて鳴らしたいときは「効果音を 1 回鳴らす」を使います。

鳴るのは、このアクションが動いた画面だけです。その場にいる全員に一度聞かせたいときは、カードの詳細設定で「全員（All）」にします。BGM のように続く音を全員でそろえるなら、同期する変数を「変数が変わったとき」で受けて、その中で鳴らしたり止めたりします（[全員で同じ状態にする](/guide/sync)）。

公式の説明: [AudioSource.Play（Unity）](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AudioSource.Play.html)
