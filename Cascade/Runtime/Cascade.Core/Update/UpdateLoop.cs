using System.Collections.Generic;
using Cascade.Service;

namespace Cascade.Core
{
    public sealed class UpdateLoop : IUpdateLoop, global::System.IDisposable
    {
        private readonly List<global::System.Action> _updates = new List<global::System.Action>();
        private readonly List<global::System.Action> _lateUpdates = new List<global::System.Action>();
        private readonly List<global::System.Action> _fixedUpdates = new List<global::System.Action>();
        private readonly ILogService _log;

        public UpdateLoop(ILogService log = null)
        {
            _log = log;
        }

        public global::System.IDisposable RegisterUpdate(global::System.Action callback)
        {
            return Register(_updates, callback);
        }

        public global::System.IDisposable RegisterLateUpdate(global::System.Action callback)
        {
            return Register(_lateUpdates, callback);
        }

        public global::System.IDisposable RegisterFixedUpdate(global::System.Action callback)
        {
            return Register(_fixedUpdates, callback);
        }

        public void TickUpdate()
        {
            InvokeAll(_updates);
        }

        public void TickLateUpdate()
        {
            InvokeAll(_lateUpdates);
        }

        public void TickFixedUpdate()
        {
            InvokeAll(_fixedUpdates);
        }

        public void Dispose()
        {
            _updates.Clear();
            _lateUpdates.Clear();
            _fixedUpdates.Clear();
        }

        private global::System.IDisposable Register(List<global::System.Action> list, global::System.Action callback)
        {
            if (callback == null)
                throw new global::System.ArgumentNullException(nameof(callback));
            list.Add(callback);
            return new Subscription(() => list.Remove(callback));
        }

        private void InvokeAll(List<global::System.Action> list)
        {
            for (var i = 0; i < list.Count; i++)
            {
                try
                {
                    list[i]?.Invoke();
                }
                catch (global::System.Exception exception)
                {
                    _log?.Exception("UpdateLoop", exception);
                }
            }
        }

        private sealed class Subscription : global::System.IDisposable
        {
            private global::System.Action _dispose;

            public Subscription(global::System.Action dispose)
            {
                _dispose = dispose;
            }

            public void Dispose()
            {
                var action = _dispose;
                _dispose = null;
                action?.Invoke();
            }
        }
    }
}

