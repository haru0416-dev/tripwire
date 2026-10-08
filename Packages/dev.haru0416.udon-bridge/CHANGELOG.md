# Changelog

## 0.1.0

- 最初の版。Tripwire から、UdonSharp と Udon の内部に触る部分を切り出しました。
- Udon のノード定義(公開されている API、変数にできる型、`==` が使える型、イベント)を読む `UdonNodes`。
- UdonSharp と同じ規則でメンバーの Udon 名を付ける `UdonNames`。継承したメンバーは呼び出す側の型の名前で、フィールドは `get_` / `set_` の名前で付けます。
- プログラムアセットの用意と、UdonBehaviour のプログラムをその場で差し替える `UdonSharpPrograms`。差し替えても、ほかからの参照と、有効・無効の状態は残ります。
- Play 中に U# だけをコンパイルし直し、動いている behaviour に新しいプログラムを入れる `HotReload`。フィールドの値は引き継ぎます。エラーで止まっていた behaviour も動き直します。実行順(`[DefaultExecutionOrder]`)を変えた変更は、Play を止めてから反映されます。
- `UdonBridge.Compiler`(Roslyn を使うアセンブリ): UdonSharp のコンパイル設定を読む `UdonSharpSettings` と、UdonSharp の bind・emit・アセンブラーをメモリ上で走らせ、保存していないコードのエラーを出す `UdonSharpCheck`。
