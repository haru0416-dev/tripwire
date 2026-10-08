using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class Counter : UdonSharpBehaviour
{
    [UdonSynced] public int count;
    public string label = "v1";

    public void Bump()
    {
        count += 1;
        RequestSerialization();
    }
}
