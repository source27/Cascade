using System;
using System.Collections.Generic;
using Cascade.Service;
using UnityEngine;

namespace Cascade.Modules.UI
{
    /// <summary>Minimal lifecycle contract for a page-owned reusable View.</summary>
    public interface IUIView : IDisposable
    {
        void Clear();
    }


    /// <summary>
    /// Owns asset handles used by a View. Clear releases current assets while keeping
    /// the scope reusable for the next Bind; Dispose permanently closes the scope.
    /// </summary>
    public sealed class UIViewAssetScope : IDisposable
    {
        private readonly HashSet<IDisposable> _handles = new HashSet<IDisposable>();
        private bool _disposed;

        public T Track<T>(T handle) where T : class, IDisposable
        {
            if (handle == null)
                return null;
            EnsureOpen();
            _handles.Add(handle);
            return handle;
        }

        public IAssetHandle<T> Replace<T>(
            ref IAssetHandle<T> current,
            IAssetHandle<T> next)
            where T : UnityEngine.Object
        {
            EnsureOpen();
            if (ReferenceEquals(current, next))
                return current;
            Release(current);
            current = next;
            if (next != null)
                _handles.Add(next);
            return next;
        }

        public void Clear()
        {
            if (_disposed)
                return;
            ReleaseAll();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            ReleaseAll();
        }

        private void ReleaseAll()
        {
            var handles = new List<IDisposable>(_handles);
            _handles.Clear();
            for (var i = handles.Count - 1; i >= 0; i--)
            {
                try
                {
                    handles[i]?.Dispose();
                }
                catch
                {
                    // Resource cleanup must continue for the remaining handles.
                }
            }
        }

        private void Release(IDisposable handle)
        {
            if (handle == null)
                return;
            _handles.Remove(handle);
            try
            {
                handle.Dispose();
            }
            catch
            {
                // Replacing one asset must not prevent the next asset from binding.
            }
        }

        private void EnsureOpen()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(UIViewAssetScope));
        }
    }
}
