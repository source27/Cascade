using System;
using Cascade.Service;
using UnityEngine;

namespace Cascade.Core
{
    /// <summary>
    /// Default <see cref="IUpdateLoop"/>: hosts itself on a hidden <c>DontDestroyOnLoad</c> GameObject and
    /// ticks its <see cref="UpdateLoop"/> core from Unity's Update/LateUpdate/FixedUpdate.
    /// <para>
    /// Replacing the loop (PlayerLoop-driven, ECS-driven, headless) is a plain
    /// <c>registry.Register/Replace&lt;IUpdateLoop&gt;(…)</c> — nothing else in the framework drives it.
    /// </para>
    /// </summary>
    public sealed class UnityUpdateLoop : MonoBehaviour, IUpdateLoop, IDisposable
    {
        public const string HostObjectName = "[Cascade] UpdateLoop";

        private UpdateLoop _core;
        private bool _disposed;

        /// <summary>Creates the loop together with its host object.</summary>
        public static UnityUpdateLoop Create(ILogService log = null)
        {
            var host = new GameObject(HostObjectName);
            if (Application.isPlaying)
                UnityEngine.Object.DontDestroyOnLoad(host); // editor-only: throws outside play mode
            var loop = host.AddComponent<UnityUpdateLoop>();
            loop._core = new UpdateLoop(log);
            return loop;
        }

        public IDisposable RegisterUpdate(Action callback) => Core().RegisterUpdate(callback);
        public IDisposable RegisterLateUpdate(Action callback) => Core().RegisterLateUpdate(callback);
        public IDisposable RegisterFixedUpdate(Action callback) => Core().RegisterFixedUpdate(callback);

        /// <summary>Stops ticking, clears callbacks and destroys the host object.</summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _core?.Dispose();
            _core = null;

            if (this == null)
                return;

            // DestroyImmediate outside play mode: EditMode callers (tests, tooling) expect the object gone
            // as soon as Dispose returns.
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(gameObject);
            else
                UnityEngine.Object.DestroyImmediate(gameObject);
        }

        private void Update() => _core?.TickUpdate();

        private void LateUpdate() => _core?.TickLateUpdate();

        private void FixedUpdate() => _core?.TickFixedUpdate();

        private UpdateLoop Core()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(UnityUpdateLoop));
            return _core ?? throw new InvalidOperationException(
                $"{nameof(UnityUpdateLoop)} must be created through {nameof(Create)}; a manually added component has no core loop.");
        }
    }
}
