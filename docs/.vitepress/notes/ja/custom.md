カードの「呼ばれる名前」に付けた名前で、ほかのトリガーの「カスタムイベントを呼ぶ」や、UdonSharp のスクリプトの `SendCustomEvent` から呼び出せます。同じトリガーの中で同じ名前を 2 つ使うことはできません。

何度も使うアクションの並びを 1 つにまとめて、いくつかのカードから呼ぶ使い方もできます。

名前は英字で始め、英数字と _ だけで付けます。Udon がすでに使っている名前（Start など）や、Tw_・tw_・v_ で始まる名前は使えません。

「カスタムイベントを呼ぶ」の「誰の画面で」を「全員（All）」か「オーナーだけ（Owner）」にすると、VRChat のネットワークのイベントとして送られます。ネットワークのイベントは、あとから来た人には届きません。送れるのはイベントごとに標準で 1 秒に 5 回までで、超えた分は送る側で順番待ちになります。全員で同じ状態を保ちたいときは、[同期する変数](/guide/sync) を使ってください。

呼び出し方の例は [ほかのトリガーやスクリプト](/guide/linking#カスタムイベントで呼ぶ) にあります。

公式の説明: [Custom Network Events（VRChat）](https://creators.vrchat.com/worlds/udon/networking/events)、[SendCustomEvent（UdonSharp）](https://udonsharp.docs.vrchat.com/vrchat-api/)
