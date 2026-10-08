using UdonSharp;
using UnityEngine;

// A stand-in for a third-party U# asset, used by ScriptCallTests: methods with arguments and results, fields, a
// reference to another object.
public class TripwireTestCounter : UdonSharpBehaviour
{
    public int count;
    public string label = "";
    public GameObject lamp;

    public void Add(int amount) { count += amount; }

    public int Doubled() { return count * 2; }

    public void HideLamp()
    {
        if (lamp != null) lamp.SetActive(false);
    }
}
