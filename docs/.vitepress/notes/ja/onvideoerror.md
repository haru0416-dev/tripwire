動画の読み込みに失敗したときに起きます。看板に「読み込めませんでした」と出すなど、見ている人への案内に使えます。「エラーの種類」で分けるときは、「くわしく」で VideoError の変数を作って「変数に値を入れる」で値を入れ、If で比べてください。

新しい URL を読み込めるのは、ひとりにつき 5 秒に 1 回までです。この制限はすべての動画プレイヤーを合わせた回数で、最初から入れてある URL にも当てはまります。動画プレイヤーが 2 台あると、あとから来た人の画面で 2 つの読み込みが同時に始まり、失敗します。読み込む時間をずらしてください。

許可リストにないサイトの動画は、見る人が設定で Allow Untrusted URLs をオンにしていないと見られません。

公式の説明: [Event Nodes（VRChat）](https://creators.vrchat.com/worlds/udon/graph/event-nodes/)、[Video Players（VRChat）](https://creators.vrchat.com/worlds/udon/video-players/)
