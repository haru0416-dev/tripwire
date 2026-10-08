このオブジェクトが有効（表示）になったときに起きます。ワールドが始まったときに有効なオブジェクトでも起きて、その順番は「開始したとき」より前です。「開始したとき」と違い、無効にしてまた有効にするたびに起きます。

「オブジェクトを表示・非表示」でオンにすると起きます。そのアクションが動いた画面でだけ有効になるので、このイベントもその画面でだけ起きます。全員の画面で有効にしたいときは、同期する変数と「変数が変わったとき」で表示を切り替えます（[全員で同じ状態にする](/guide/sync)）。

カードの詳細設定は「自分だけ（Local）」のままにしてください。ほかにすると、起きるたびに人数分くり返し動きます。VRC Object Pool から取り出されたときも、このイベントを使います。

公式の説明: [MonoBehaviour.OnEnable（Unity）](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.OnEnable.html)、[Order of execution for event functions（Unity）](https://docs.unity3d.com/2022.3/Documentation/Manual/ExecutionOrder.html)
