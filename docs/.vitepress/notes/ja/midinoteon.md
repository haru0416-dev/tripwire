MIDI の Note On を受けたときに起きます。つないだ MIDI 機器で鍵盤やボタンを押したときと、VRC Midi Player で MIDI データを再生しているときです。

機器から受けるには、シーンのどこかに VRC Midi Listener を置きます。Active Events で受けるイベントをオンにし（最初はどれもオフです）、Behaviour にトリガーの付いたオブジェクトを入れてください。Tripwire はこの設定を自動では行いません。再生で受けるときは、VRC Midi Player の Target Behaviours に入れます。

「チャンネル」は 0〜15、「番号」は 0〜127 の音の番号です。「強さ」は鍵盤を押した速さで、0〜127 です。機器によっては、番号の全部や強さが出ないことがあります。

VRChat は、PC で見つけた最初の MIDI 機器を開きます。別の機器を使うときは、起動オプションに `--midi=機器の名前の一部` を足します。

公式の説明: [Midi in Udon（VRChat）](https://creators.vrchat.com/worlds/udon/midi/)、[Realtime Midi（VRChat）](https://creators.vrchat.com/worlds/udon/midi/realtime-midi/)、[Midi Playback（VRChat）](https://creators.vrchat.com/worlds/udon/midi/midi-playback/)
