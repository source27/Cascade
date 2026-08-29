using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cascade.Service;

namespace Cascade.Core
{
    public sealed class LifecycleRunner
    {
        private readonly List<ILifecycleModule> _modules = new List<ILifecycleModule>();
        private readonly ILogService _log;
        private bool _started;
        private bool _snapshotLocked;

        public LifecycleRunner(ILogService log = null)
        {
            _log = log;
        }

        public void Register(ILifecycleModule module)
        {
            if (module == null)
                throw new global::System.ArgumentNullException(nameof(module));
            if (_snapshotLocked)
                throw new global::System.InvalidOperationException("Cannot register modules while Initialize is running.");
            _modules.Add(module);
        }

        public async UniTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            _snapshotLocked = true;
            var snapshot = _modules.ToArray();
            var initialized = new List<ILifecycleModule>(snapshot.Length);

            try
            {
                foreach (var module in snapshot)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await module.InitializeAsync(cancellationToken);
                    initialized.Add(module);
                }
            }
            catch
            {
                for (var i = initialized.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        initialized[i].Stop();
                    }
                    catch (global::System.Exception exception)
                    {
                        _log?.Exception("Lifecycle", exception, $"Stop after failed init: {initialized[i].Name}");
                    }
                }

                _snapshotLocked = false;
                throw;
            }

            _snapshotLocked = false;
        }

        public void StartAll()
        {
            if (_started)
                return;

            foreach (var module in _modules)
                module.Start();
            _started = true;
        }

        public void StopAll()
        {
            for (var i = _modules.Count - 1; i >= 0; i--)
                _modules[i].Stop();
            _started = false;
        }

        public void ResetAll()
        {
            for (var i = _modules.Count - 1; i >= 0; i--)
                _modules[i].Reset();
            _started = false;
        }
    }
}
