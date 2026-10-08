カードの「対象の UI」に入れたトグルが切り替わったときに起きます。配線はボタンと同じで、変換のときに Tripwire がトグルの On Value Changed へ足します（[ボタンが押されたとき](/reference/events/uibuttonclick)）。

「イベントの値」は、イベントが起きたときにトグルの isOn を読んだ値です。切り替わった後の状態なので、そのまま「オブジェクトを表示・非表示」の「表示する」に選べば、トグルと表示がそろいます。オンとオフで別のことをするときは、「変数に値を入れる」で変数に入れて If で分けます。

Unity の Toggle には、On Value Changed を呼ばずに値を変える SetIsOnWithoutNotify が別にあります。つまり isOn を直接変えると、スクリプトから変えたときもこのイベントが起きます。

公式の説明: [Toggle（Unity UI）](https://docs.unity3d.com/Packages/com.unity.ugui@1.0/manual/script-Toggle.html)、[Toggle API（Unity UI）](https://docs.unity3d.com/Packages/com.unity.ugui@1.0/api/UnityEngine.UI.Toggle.html)、[VRC Ui Shape（VRChat）](https://creators.vrchat.com/worlds/components/vrc_uishape/)
