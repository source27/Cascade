using System;
using UnityEngine;

namespace Cascade.Modules.UI
{
    public sealed class UIBindingHost : MonoBehaviour
    {
        [SerializeField] private UnityEngine.Object[] _objects = Array.Empty<UnityEngine.Object>();

        public int Count => _objects?.Length ?? 0;

        public T Get<T>(int index) where T : UnityEngine.Object
        {
            if (_objects == null || index < 0 || index >= _objects.Length)
                throw new IndexOutOfRangeException($"UI binding index is invalid: {index}");
            if (!(_objects[index] is T value))
                throw new InvalidOperationException(
                    $"UI binding type mismatch at {index}. Expected {typeof(T).FullName}.");
            return value;
        }
    }
}
