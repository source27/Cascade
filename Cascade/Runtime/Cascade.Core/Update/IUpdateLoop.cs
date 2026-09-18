namespace Cascade.Core
{
    public interface IUpdateLoop
    {
        global::System.IDisposable RegisterUpdate(global::System.Action callback);
        global::System.IDisposable RegisterLateUpdate(global::System.Action callback);
        global::System.IDisposable RegisterFixedUpdate(global::System.Action callback);

        // Driver side: whoever owns the Unity callbacks drives the loop (see UnityUpdateDriver).
        void TickUpdate();
        void TickLateUpdate();
        void TickFixedUpdate();
    }
}

