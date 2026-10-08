対象のオブジェクトのオーナーを、このアクションが動いた画面のプレイヤーにします。オーナーはオブジェクトごとに 1 人いて、そのオブジェクトの同期する変数を変えられるのはオーナーだけです。最初のオーナーは、インスタンスに最初に入った人です。

Tripwire の同期する変数は、値を変えた人が自動でオーナーになるので、変数のためにこのアクションを置く必要はありません。使うのは、「オーナーだけ（Owner）」で送るイベントを自分に届けたいときや、オーナーを前提に動くほかのスクリプトを使うときです。

オーナーが変わると、インスタンスにいる全員の画面で「[オーナーが変わったとき](/reference/events/onownershiptransferred)」が起きます。オーナーが退出したときは、VRChat が自動で新しいオーナーを決めます。そのオブジェクトのスクリプトが OnOwnershipRequest で断ったときは、オーナーは移りません。

公式の説明: [Object Ownership（VRChat）](https://creators.vrchat.com/worlds/udon/networking/ownership/)、[Network Components（VRChat）](https://creators.vrchat.com/worlds/udon/networking/network-components/)
