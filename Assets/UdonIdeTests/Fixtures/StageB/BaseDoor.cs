using UdonSharp;

public abstract class BaseDoor : UdonSharpBehaviour
{
    public int opens;

    public virtual void Open()
    {
        opens++;
    }

    public abstract void Close();
}
