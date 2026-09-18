using System;
using System.Collections.Generic;
using Cascade.Service;
using UnityEngine;
using UnityEngine.LowLevel;

namespace Cascade.Core
{
    /// <summary>
    /// GameObject-free <see cref="IUpdateLoop"/>: injects its ticks into Unity's PlayerLoop
    /// (right after the behaviour phases, so registered callbacks run once per frame / fixed step).
    /// <para>
    /// Trade-off versus <see cref="UnityUpdateLoop"/>: no host object, but ticks land <b>after</b> every
    /// MonoBehaviour instead of interleaved with them, and the injection lives in runtime-global state
    /// (removed on <see cref="Dispose"/>). Anchors are Unity's own phase types; when one is missing the
    /// injection is skipped and an error is logged instead of failing silently.
    /// </para>
    /// </summary>
    public sealed class PlayerLoopUpdateLoop : IUpdateLoop, IDisposable
    {
        private readonly UpdateLoop _core;
        private readonly PlayerLoopSystem _updateSystem;
        private readonly PlayerLoopSystem _lateUpdateSystem;
        private readonly PlayerLoopSystem _fixedUpdateSystem;
        private bool _injected;
        private bool _disposed;

        public PlayerLoopUpdateLoop(ILogService log = null)
        {
            _core = new UpdateLoop(log);
            _updateSystem = CreateSystem(TickUpdate);
            _lateUpdateSystem = CreateSystem(TickLateUpdate);
            _fixedUpdateSystem = CreateSystem(TickFixedUpdate);
            Inject(log);
        }

        /// <summary>True when the ticks are currently part of Unity's PlayerLoop.</summary>
        public bool IsInjected => _injected;

        public IDisposable RegisterUpdate(Action callback) => Core().RegisterUpdate(callback);
        public IDisposable RegisterLateUpdate(Action callback) => Core().RegisterLateUpdate(callback);
        public IDisposable RegisterFixedUpdate(Action callback) => Core().RegisterFixedUpdate(callback);

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            if (_injected)
                Remove();
            _core.Dispose();
        }

        private void TickUpdate() => _core.TickUpdate();

        private void TickLateUpdate() => _core.TickLateUpdate();

        private void TickFixedUpdate() => _core.TickFixedUpdate();

        private UpdateLoop Core()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(PlayerLoopUpdateLoop));
            return _core;
        }

        private static PlayerLoopSystem CreateSystem(PlayerLoopSystem.UpdateFunction tick) =>
            new PlayerLoopSystem
            {
                type = typeof(PlayerLoopUpdateLoop),
                updateDelegate = tick
            };

        private void Inject(ILogService log)
        {
            var loop = UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop();
            var inserted =
                InsertAfter(ref loop, typeof(UnityEngine.PlayerLoop.Update.ScriptRunBehaviourUpdate), _updateSystem) &
                InsertAfter(ref loop, typeof(UnityEngine.PlayerLoop.PreLateUpdate.ScriptRunBehaviourLateUpdate), _lateUpdateSystem) &
                InsertAfter(ref loop, typeof(UnityEngine.PlayerLoop.FixedUpdate.ScriptRunBehaviourFixedUpdate), _fixedUpdateSystem);

            if (!inserted)
            {
                log?.Error(nameof(PlayerLoopUpdateLoop),
                    "PlayerLoop anchors not found; the loop was not injected. Use UnityUpdateLoop instead.");
                return;
            }

            UnityEngine.LowLevel.PlayerLoop.SetPlayerLoop(loop);
            _injected = true;
        }

        private void Remove()
        {
            var loop = UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop();
            FilterOut(ref loop, this);
            UnityEngine.LowLevel.PlayerLoop.SetPlayerLoop(loop);
            _injected = false;
        }

        private static bool InsertAfter(ref PlayerLoopSystem root, Type anchor, PlayerLoopSystem item)
        {
            if (root.subSystemList == null)
                return false;

            for (var i = 0; i < root.subSystemList.Length; i++)
            {
                if (root.subSystemList[i].type != anchor)
                    continue;

                root.subSystemList = Insert(root.subSystemList, i + 1, item);
                return true;
            }

            for (var i = 0; i < root.subSystemList.Length; i++)
            {
                var child = root.subSystemList[i];
                if (!InsertAfter(ref child, anchor, item))
                    continue;

                root.subSystemList[i] = child;
                return true;
            }

            return false;
        }

        private static PlayerLoopSystem[] Insert(PlayerLoopSystem[] list, int index, PlayerLoopSystem item)
        {
            var result = new PlayerLoopSystem[list.Length + 1];
            Array.Copy(list, 0, result, 0, index);
            result[index] = item;
            Array.Copy(list, index, result, index + 1, list.Length - index);
            return result;
        }

        /// <summary>
        /// Drops the systems owned by <paramref name="owner"/>. Ownership is matched through the delegate
        /// target (not the marker type) so several loop instances stay independent.
        /// </summary>
        private static bool FilterOut(ref PlayerLoopSystem root, object owner)
        {
            if (root.subSystemList == null)
                return false;

            var kept = new List<PlayerLoopSystem>(root.subSystemList.Length);
            var changed = false;
            foreach (var child in root.subSystemList)
            {
                if (child.updateDelegate != null && ReferenceEquals(child.updateDelegate.Target, owner))
                {
                    changed = true;
                    continue;
                }

                var copy = child;
                if (FilterOut(ref copy, owner))
                    changed = true;
                kept.Add(copy);
            }

            if (changed)
                root.subSystemList = kept.ToArray();

            return changed;
        }
    }
}
