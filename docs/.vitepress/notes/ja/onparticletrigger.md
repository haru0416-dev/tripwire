パーティクルシステムの Triggers モジュールをオンにすると、そのパーティクルシステムのオブジェクトに付いたトリガーで起きるようになります。粒が Triggers モジュールの条件を満たしたときに起きます。

条件は、モジュールの Colliders に入れたコライダーと粒の関係（Inside・Outside・Enter・Exit）ごとに、Ignore・Kill・Callback から選んで決めます。Callback にした粒はこのイベントで扱え、Kill にした粒は消えて扱えなくなります。Ignore は何もしません。

どの粒がどのコライダーに触れたかは、このイベントの値には入っていません。細かく扱うには、スクリプトで GetTriggerParticles を使います。

公式の説明: [OnParticleTrigger（Unity）](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.OnParticleTrigger.html)、[Triggers module（Unity）](https://docs.unity3d.com/2022.3/Documentation/Manual/PartSysTriggersModule.html)
