VRCAsyncGPUReadback.Request で頼んだ、GPU 上のテクスチャの読み取りが終わったときに起きます。届くのは、頼むときに指定した UdonBehaviour だけです。読み取りはメインの処理を止めずに進むので、フレームレートを落としません。

「読み取り結果」は、まず hasError で失敗していないか確かめます。中身は GetData でなく TryGetData で取り出します。

Tripwire には、読み取りを頼むアクションがありません。このイベントは、ほかの UdonSharp スクリプトがこのトリガーを受け取り先にして頼んだときに起きます。

公式の説明: [AsyncGPUReadback（VRChat）](https://creators.vrchat.com/worlds/udon/vrc-graphics/asyncgpureadback/)
