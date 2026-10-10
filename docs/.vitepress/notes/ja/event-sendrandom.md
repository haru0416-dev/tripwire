「呼ぶイベント」に書いたカスタムイベントの名前から 1 つをランダムに選んで呼びます。名前は 1 行に 1 つずつか、「,」で区切って書きます。くじ引きや、毎回違う演出を出すときに便利です。

選ぶのは 1 回だけで、対象が何個あっても同じイベントが届きます。「誰の画面で」は「[カスタムイベントを呼ぶ](/reference/actions/event-send)」と同じです。「全員（All）」にすると、選ぶのはこのアクションが動いた画面で 1 回だけで、選ばれたイベントが全員の画面で動きます。ただし、あとから来た人には届かないので、結果を残したいときは同期する変数に入れてください。

対象をこのトリガー自身にしたとき、書いた名前のカスタムイベントがなければ注意が出ます。選ばれる確率は、どの名前も同じです。

公式の説明: [Random.Range（Unity）](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Random.Range.html)、[Network Events（VRChat）](https://creators.vrchat.com/worlds/udon/networking/events/)
