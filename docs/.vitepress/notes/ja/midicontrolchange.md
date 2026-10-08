MIDI のコントロールチェンジを受けたときに起きます。たいていは、MIDI 機器のつまみやスライダーを動かしたときです。VRC Midi Listener の Active Events で、このイベントもオンにしてください（[MIDI の鍵盤を押したとき](/reference/events/midinoteon)）。VRC Midi Player が送るのは Note On と Note Off だけです。

「チャンネル」は 0〜15、「番号」はコントロールの番号で 0〜127、「イベントの値」は 0〜127 です。端のない回り続けるつまみでは、値が 0 と 1 のように増減の向きだけを表すことがあります。その場合は「変数に足す」などで、自分で値を積み上げます。

公式の説明: [Midi in Udon（VRChat）](https://creators.vrchat.com/worlds/udon/midi/)、[Midi Playback（VRChat）](https://creators.vrchat.com/worlds/udon/midi/midi-playback/)
