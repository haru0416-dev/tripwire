using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Data;

// 打ちかけの途中状態を作るための、機能をいろいろ使ったファイル
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class Door : BaseDoor
{
    public GameObject panel;
    public Helper helper;
    public Mode mode = Mode.Closed;
    [UdonSynced, FieldChangeCallback(nameof(Count))] int _count;
    [UdonSynced] bool open;
    DataList history = new DataList();
    int 開いた回数;

    public int Count
    {
        get => _count;
        set { _count = value; panel.SetActive(value % 2 == 0); }
    }

    public override void Open()
    {
        base.Open();
        Count++;
        開いた回数 += helper != null ? helper.Twice(1) : 1;
        history.Add($"open {Count:D3} {mode}");
    }

    public override void Close()
    {
        switch (mode)
        {
            case Mode.Open: mode = Mode.Closed; break;
            case Mode.Locked: return;
            default: mode = Mode.Open; break;
        }
        RequestSerialization();
    }

    public override void Interact()
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        open = !open;
        if (open) Open(); else Close();
        var players = new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()];
        VRCPlayerApi.GetPlayers(players);
        foreach (var p in players)
        {
            if (!Utilities.IsValid(p)) continue;
            for (int i = 0; i < 3; i++) { if (p.isLocal && i == 1) SendCustomEventDelayedSeconds(nameof(Later), 0.5f * i); }
        }
    }

    [RecursiveMethod]
    int Fib(int n) { return n < 2 ? n : Fib(n - 1) + Fib(n - 2); }

    public void Later()
    {
        int[][] grid = new int[2][];
        grid[0] = new int[] { Fib(5), Helper.Square(3) };
        Debug.Log(string.Join(",", history.Count.ToString(), grid[0][1].ToString()));
    }
}
