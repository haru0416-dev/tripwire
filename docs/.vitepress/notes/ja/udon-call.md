くわしくモードの「上級」にあるアクションです。「呼び出すもの」で、Udon から使えるメソッドとプロパティを検索して選びます。一覧は Udon 自身の定義から作られ、Udon に公開されていないものは出てきません。入力欄の型を Tripwire がまだ扱えないものも、一覧から外れます。

どれが Udon に公開されているかは、VRChat SDK の Class Exposure Tree（VRChat SDK > Udon Sharp > Class Exposure Tree）でも確かめられます。緑が公開されているもの、赤が公開されていないものです。

値を返すものは、「結果を入れる変数」に入れられます。プロパティを読むときは、入れる変数を必ず選んでください。対象を何個も入れたときは、最後の対象の値が変数に残ります。

`out` の引数があるもの（`int.TryParse` や `Physics.Raycast`、PlayerData の `TryGetInt` など）は、その引数の欄で受け取る変数を選びます。欄には「（結果）」と出ていて、呼んだあとの値がその変数に入る形です。成功したかを返すものは、その値を「結果を入れる変数」に入れて、If で分けてください。`ref` の引数があるもの（`Mathf.SmoothDamp` など）は、Udon では値が戻らないので一覧に出ません。

結果を知らせる先（受け取り先）を渡すもの（`VRCStringDownloader.LoadUrl`、`VRCTween` の終わったら呼ぶ版、値付きの `SendCustomNetworkEvent`、`Store.ListPurchases` など）は、その欄に最初から「このオブジェクト」が入っています。結果はこのトリガーのイベント（「文字の読み込みが終わったとき」など）や、名前で指定したカスタムイベントに届きます。VRCTween の「variableName」に書くのは、このトリガーの変数の名前そのままで大丈夫です。受け取り先をほかのスクリプトにしたときは、そのスクリプトの中の変数名を書いてください。

位置や色の一部を変えるもの（`Vector3.y` を変える、`Color.a` を変える、`Normalize` など）と、`VRCTweenHandle` の `Kill` などは、対象に変数を選んだときだけ使えます。変わった値はその変数に入れ直されます。

Udon が変数に持てない列挙型を返すもの（`VRCPickup.currentHand`、`Renderer.shadowCastingMode` など）は、数として整数の変数に入ります。どの数が何を表すかは、結果の欄の下に出ます。

位置や色などの変数を対象にしてメソッドを呼んだり、プロパティを変えたりすると、変わった値はその変数に入れ直されます。同期する変数なら値が全員に届き、「変数が変わったとき」も動きます。

オブジェクトに対して呼んだものは、このアクションが動いた画面でだけ動きます（[ほかのトリガーやスクリプト](/guide/linking)）。

公式の説明: [Class Exposure Tree（VRChat）](https://creators.vrchat.com/worlds/udon/udonsharp/class-exposure-tree)
