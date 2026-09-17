using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cascade.Integrations.Desktop
{
    /// <summary>
    /// 轻量字符串 ID 状态机（Enter / Exit / Update 委托）。
    /// 可选 PC shell 辅助；多数游戏更宜用 Cascade.Core GameFlow，勿与之强绑定。
    /// </summary>
    public sealed class SimpleStateMachine
    {
        public sealed class State
        {
            public string Id;
            public Action OnEnter;
            public Action OnExit;
            public Action OnUpdate;
        }

        readonly Dictionary<string, State> _states = new Dictionary<string, State>(StringComparer.Ordinal);

        public string CurrentId { get; private set; }
        public string PreviousId { get; private set; }

        public event Action<string, string> OnStateChanged;

        public void Register(string id, Action onEnter = null, Action onExit = null, Action onUpdate = null)
        {
            if (string.IsNullOrEmpty(id))
                return;
            _states[id] = new State
            {
                Id = id,
                OnEnter = onEnter,
                OnExit = onExit,
                OnUpdate = onUpdate
            };
        }

        public void TransitionTo(string nextId)
        {
            if (string.IsNullOrEmpty(nextId) || nextId == CurrentId)
                return;
            if (!_states.ContainsKey(nextId))
            {
                Debug.LogWarning($"[SimpleStateMachine] Unknown state: {nextId}");
                return;
            }

            var from = CurrentId;
            if (!string.IsNullOrEmpty(from) && _states.TryGetValue(from, out var prev))
            {
                try { prev.OnExit?.Invoke(); }
                catch (Exception e) { Debug.LogWarning($"[SimpleStateMachine] Exit {from}: {e.Message}"); }
            }

            PreviousId = from;
            CurrentId = nextId;

            if (_states.TryGetValue(nextId, out var next))
            {
                try { next.OnEnter?.Invoke(); }
                catch (Exception e) { Debug.LogWarning($"[SimpleStateMachine] Enter {nextId}: {e.Message}"); }
            }

            OnStateChanged?.Invoke(from, nextId);
        }

        public void Update()
        {
            if (string.IsNullOrEmpty(CurrentId))
                return;
            if (_states.TryGetValue(CurrentId, out var s))
            {
                try { s.OnUpdate?.Invoke(); }
                catch (Exception e) { Debug.LogWarning($"[SimpleStateMachine] Update {CurrentId}: {e.Message}"); }
            }
        }
    }
}
