自分が VRChat の表示言語を変えたときに起きます。ワールドに入ったときにも起きるので、看板の文字を言語に合わせる処理はここにまとめられます。

言語は en や ja、zh-CN のような RFC 5646 の形で表されます（今の言語を返す GetCurrentLanguage の説明より）。「変数に値を入れる」で「言語コード」を文字の変数に入れ、If で ja などと比べて「テキストを変える」の文字を分けてください。

公式の説明: [Event Nodes（VRChat）](https://creators.vrchat.com/worlds/udon/graph/event-nodes/)、[Release 3.7.0（VRChat）](https://creators.vrchat.com/releases/release-3-7-0/)、[Player API（VRChat）](https://creators.vrchat.com/worlds/udon/players/)
