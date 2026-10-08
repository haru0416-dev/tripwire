With the particle system's Triggers module on, a trigger on the particle system's own object gets this event when particles meet the module's conditions.

The conditions are set per relation between particles and the colliders in the module's Colliders list (Inside, Outside, Enter, Exit), each set to Ignore, Kill or Callback. Particles set to Callback can be handled in this event; Kill destroys them, and they can't be handled; Ignore does nothing.

The event's value doesn't say which particle touched which collider. For that, a script uses GetTriggerParticles.

Official docs: [OnParticleTrigger (Unity)](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.OnParticleTrigger.html), [Triggers module (Unity)](https://docs.unity3d.com/2022.3/Documentation/Manual/PartSysTriggersModule.html)
