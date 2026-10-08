using UdonSharp;
using UnityEngine;

public class Helper : UdonSharpBehaviour
{
    public int value;

    public int Twice(int x)
    {
        return x * 2;
    }

    public void Ping()
    {
        value++;
    }

    public static int Square(int x)
    {
        return x * x;
    }
}
