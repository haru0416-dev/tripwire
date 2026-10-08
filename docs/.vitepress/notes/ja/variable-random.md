1〜6 にすれば、6 も出ます。

同期する変数に入れると、出た数が全員に届きます。全員で同じ結果を見せたいサイコロやくじは、この形にします（[サイコロ](/recipes/dice)）。

小数の変数なら、最小と最大のどちらの値も出ることがあります。整数のときに最大も出るのは、Unity の Random.Range が整数では最大を含まないため、Tripwire が最大に 1 を足して呼んでいるからです。最小と最大を数で入れたとき、最小のほうが大きいとエラーになります。

公式の説明: [Random.Range（Unity）](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Random.Range.html)
