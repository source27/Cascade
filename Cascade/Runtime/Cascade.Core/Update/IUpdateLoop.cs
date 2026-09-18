namespace Cascade.Core
{
    /// <summary>
    /// Frame loop contract: subscriber-facing only. Register a callback and keep the returned token —
    /// <b>disposing the token is how you unregister</b> (an explicit Unregister(callback) overload is
    /// deliberately absent: it can only match method groups reliably, while lambda captures would fail
    /// silently; the token is exact, idempotent, and works for any delegate).
    /// <para>
    /// How time is driven is an implementation detail: <see cref="UpdateLoop"/> is a plain C# core (driven by
    /// whoever owns it), <see cref="UnityUpdateLoop"/> hosts itself on a hidden DontDestroyOnLoad object, and
    /// <see cref="PlayerLoopUpdateLoop"/> injects into Unity's PlayerLoop with no GameObject at all.
    /// </para>
    /// </summary>
    public interface IUpdateLoop
    {
        global::System.IDisposable RegisterUpdate(global::System.Action callback);
        global::System.IDisposable RegisterLateUpdate(global::System.Action callback);
        global::System.IDisposable RegisterFixedUpdate(global::System.Action callback);
    }
}
