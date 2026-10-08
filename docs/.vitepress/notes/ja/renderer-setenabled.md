見た目（レンダラー）だけをオン・オフします。オブジェクトそのものは動き続けるので、見えなくしても当たり判定やトリガーはそのまま動きます。

切り替わるのは、このアクションが動いた画面だけです。全員の画面でそろえたいときは、同期する変数を「変数が変わったとき」で受けて、その中でこのアクションを動かします（[全員で同じ状態にする](/guide/sync)）。

公式の説明: [Renderer.enabled（Unity）](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Renderer-enabled.html)
