カードの「対象の UI」に入れたスライダーが動いたときに起きます。配線はボタンと同じで、変換のときに Tripwire がスライダーの On Value Changed へ足します（[ボタンが押されたとき](/reference/events/uibuttonclick)）。つまみをドラッグしている間は、値が変わるたびに起きます。

「イベントの値」は、スライダーの value を読んだ値です。範囲はスライダーの Min Value から Max Value までです。Whole Numbers をオンにしても、値は小数として届きます。

移動のキーやスティックでスライダーが動いてしまうときは、スライダーの Navigation を None にします。Unity の Slider には、On Value Changed を呼ばずに値を変える SetValueWithoutNotify も別にあります。

公式の説明: [Slider（Unity UI）](https://docs.unity3d.com/Packages/com.unity.ugui@1.0/manual/script-Slider.html)、[Slider API（Unity UI）](https://docs.unity3d.com/Packages/com.unity.ugui@1.0/api/UnityEngine.UI.Slider.html)、[VRC Ui Shape（VRChat）](https://creators.vrchat.com/worlds/components/vrc_uishape/)
