「URL」の動画を読み込んで再生します。VRChat では、URL（VRCUrl）は実行中に作れず、エディタで決めておくものです。そのため「URL」には、Inspector で書いた URL か、URL の変数を入れます。文字をつなげて URL を作ることはできません。

新しい URL を読み込めるのは、1 人につき 5 秒に 1 回までです。この制限はワールドのすべての動画プレイヤーで共通で、動画プレイヤーに最初から入れてある URL も数に入ります。動画プレイヤーが 2 台あると、あとから来た人は 2 本読み込むことになり、同時だと失敗します。

再生されるのは、このアクションが動いた画面だけです。VRChat の動画プレイヤーは、それだけでは同期しません。カードの詳細設定を「全員（All）」にすればその場の全員で再生できますが、あとから来た人には届きません。あとから来た人にもそろえたいときは、ProTV などの同期のしくみを持つプレイヤーを使います（[動画プレイヤー](/guide/video-players)、[シアター](/recipes/theater)）。

YouTube、Twitch、Vimeo など、VRChat の許可リストにあるサイトの動画は、そのまま再生できます。ほかのサイトは、見る人が設定で Allow Untrusted URLs をオンにしている必要があります。Public と Group Public のインスタンスでは、許可リストにないサイトのうち、再生できるのはワールドの Video Player Allowed Domains に入れたドメインだけです。Android では、HTTPS でないサイトの動画は再生されません。

Unity の Play で試せるのは、VRC Unity Video Player に mp4 や webm のファイルを直接指す URL を入れたときです。YouTube などのサービスは VRChat の中でしか再生されません。VRC AVPro Video Player はエディタでは再生しないので、Build & Test で試します。ライブ配信を流せるのは AVPro のほうです。

公式の説明: [Video Players（VRChat）](https://creators.vrchat.com/worlds/udon/video-players/)、[Video Player Allowlist（VRChat）](https://creators.vrchat.com/worlds/udon/video-players/www-whitelist)、[VRCUrl（UdonSharp）](https://udonsharp.docs.vrchat.com/vrchat-api)
