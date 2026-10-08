using UdonSharp;
using UnityEngine;

// A stand-in for a third-party U# asset that notifies listeners (the shapes ProTV and VideoTXL use), used by ListenTests.
public class TripwireTestNotifier : UdonSharpBehaviour
{
    UdonSharpBehaviour[] listeners = new UdonSharpBehaviour[0];
    UdonSharpBehaviour[] named = new UdonSharpBehaviour[0];
    string[] names = new string[0];

    // ProTV style: register the behaviour, then it is called back by fixed names.
    public void _RegisterListener(UdonSharpBehaviour listener, sbyte priority = 0)
    {
        listeners = Append(listeners, listener);
    }

    // VideoTXL style: the listener chooses the name it is called back by.
    public void _RegisterNamed(int eventIndex, UdonSharpBehaviour handler, string eventName)
    {
        if (eventIndex != 7) return;
        named = Append(named, handler);
        var n = new string[names.Length + 1];
        names.CopyTo(n, 0);
        n[names.Length] = eventName;
        names = n;
    }

    public void _Notify()
    {
        foreach (var l in listeners) l.SendCustomEvent("_TvPlay");
        for (int i = 0; i < named.Length; i++) named[i].SendCustomEvent(names[i]);
    }

    static UdonSharpBehaviour[] Append(UdonSharpBehaviour[] a, UdonSharpBehaviour b)
    {
        var n = new UdonSharpBehaviour[a.Length + 1];
        a.CopyTo(n, 0);
        n[a.Length] = b;
        return n;
    }
}
