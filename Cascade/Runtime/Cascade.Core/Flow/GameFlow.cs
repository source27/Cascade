using System;
using System.Collections.Generic;
using System.Threading;
using Cascade.Service;
using Cysharp.Threading.Tasks;

namespace Cascade.Core
{
    /// <summary>
    /// Thin flow runner: register game <see cref="IGameFlowState"/>s, then
    /// <see cref="RunAsync"/> / <see cref="ChangeStateAsync"/> / <see cref="ReturnAsync"/>.
    /// Exit previous, Enter next. Keeps a single previous-id slot (not a stack).
    /// </summary>
    public sealed class GameFlow : IGameFlowQuery, IDisposable
    {
        private readonly Dictionary<string, IGameFlowState> _states;
        private readonly ILogService _log;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private IGameFlowState _current;
        private string _previousStateId;
        private bool _transitioning;
        private bool _disposed;

        public GameFlow(IEnumerable<IGameFlowState> states, ILogService log = null)
        {
            if (states == null)
                throw new ArgumentNullException(nameof(states));

            _states = new Dictionary<string, IGameFlowState>(StringComparer.Ordinal);
            foreach (var state in states)
            {
                if (state == null)
                    throw new ArgumentException("Flow state list cannot contain null.", nameof(states));
                if (string.IsNullOrWhiteSpace(state.Id))
                    throw new ArgumentException("Flow state id is required.", nameof(states));
                if (_states.ContainsKey(state.Id))
                    throw new ArgumentException($"Duplicate flow state id '{state.Id}'.", nameof(states));
                _states.Add(state.Id, state);
            }

            if (_states.Count == 0)
                throw new ArgumentException("At least one flow state is required.", nameof(states));

            _log = log;
        }

        public string CurrentStateId => _current?.Id;

        public string PreviousStateId => _previousStateId;

        public bool CanReturn => !string.IsNullOrEmpty(_previousStateId);

        public bool IsTransitioning => _transitioning;

        public IReadOnlyCollection<string> RegisteredStateIds => _states.Keys;

        /// <summary>Enter <paramref name="initialStateId"/> (typically after managers are ready).</summary>
        public UniTask RunAsync(string initialStateId, CancellationToken cancellationToken = default)
        {
            return ChangeStateAsync(initialStateId, cancellationToken);
        }

        /// <summary>
        /// Return to <see cref="PreviousStateId"/>. After success, previous becomes the state just left
        /// (single-slot history — not a multi-level back stack).
        /// </summary>
        public UniTask ReturnAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(_previousStateId))
                throw new InvalidOperationException("No previous flow state to return to.");
            return ChangeStateAsync(_previousStateId, cancellationToken);
        }

        public async UniTask ChangeStateAsync(string stateId, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(stateId))
                throw new ArgumentException("State id is required.", nameof(stateId));
            if (!_states.TryGetValue(stateId, out var next))
                throw new InvalidOperationException($"Unknown flow state '{stateId}'.");

            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                ThrowIfDisposed();
                if (_current != null && string.Equals(_current.Id, stateId, StringComparison.Ordinal))
                    return;

                _transitioning = true;
                var fromId = _current?.Id;
                try
                {
                    if (_current != null)
                        await _current.ExitAsync(cancellationToken);

                    _current = next;
                    await _current.EnterAsync(cancellationToken);
                    _previousStateId = fromId;
                    _log?.Info("GameFlow", fromId == null
                        ? $"Enter {stateId}"
                        : $"Transition {fromId} -> {stateId}");
                }
                catch
                {
                    // Leave _current as-is relative to how far Exit/Enter got; caller handles retry.
                    throw;
                }
                finally
                {
                    _transitioning = false;
                }
            }
            finally
            {
                _gate.Release();
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _gate.Dispose();
            _current = null;
            _previousStateId = null;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(GameFlow));
        }
    }
}
