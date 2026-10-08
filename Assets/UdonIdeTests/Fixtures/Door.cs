using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 鍵を持っている人だけが開けられる扉
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class Door : UdonSharpBehaviour
{
    public GameObject panel;
    [UdonSynced] bool open;

    public override void Interact()
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        open = !open;
        RequestSerialization();
        panel.SetActive(!open); /* 閉じた板を消す */
        Debug.Log("door " + open + " count=" + 3.5f);
    }
}
