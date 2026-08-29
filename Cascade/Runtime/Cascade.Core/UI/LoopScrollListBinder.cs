using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Cascade.Core
{
    // Virtual list adapter. Cell presentation is pure C# (not MonoBehaviour),
    // created by the page via cellFactory and cached per GameObject instance.
    public sealed class LoopScrollListBinder<TItem, TCell> : LoopScrollPrefabSource, LoopScrollDataSource, LoopScrollSizeHelper, IDisposable
        where TCell : class, ILoopScrollCellView<TItem>
    {
        private readonly LoopScrollRect _scroll;
        private readonly GameObject _cellPrefab;
        private readonly Transform _poolRoot;
        private readonly Func<GameObject, TCell> _cellFactory;
        private readonly Stack<GameObject> _pool = new Stack<GameObject>();
        private readonly Dictionary<int, TCell> _cells = new Dictionary<int, TCell>();
        private readonly Dictionary<int, float> _itemSizes = new Dictionary<int, float>();
        private IReadOnlyList<TItem> _items = Array.Empty<TItem>();
        private float _defaultSize = 216f;
        private bool _disposed;

        public LoopScrollListBinder(
            LoopScrollRect scroll,
            GameObject cellPrefab,
            Func<GameObject, TCell> cellFactory,
            Transform poolRoot = null,
            float defaultSize = 216f)
        {
            _scroll = scroll ?? throw new ArgumentNullException(nameof(scroll));
            _cellPrefab = cellPrefab ?? throw new ArgumentNullException(nameof(cellPrefab));
            _cellFactory = cellFactory ?? throw new ArgumentNullException(nameof(cellFactory));
            _poolRoot = poolRoot != null ? poolRoot : scroll.transform;
            _defaultSize = defaultSize > 1f ? defaultSize : 216f;
            _scroll.prefabSource = this;
            _scroll.dataSource = this;
            _scroll.sizeHelper = this;
        }

        public IReadOnlyList<TItem> Items => _items;
        public int Count => _items.Count;

        public void SetData(IReadOnlyList<TItem> items, bool refill = true, bool fromEnd = false)
        {
            EnsureOpen();
            _items = items ?? Array.Empty<TItem>();
            _scroll.totalCount = _items.Count;
            if (_items.Count == 0)
            {
                _scroll.ClearCells();
                return;
            }

            if (!refill)
            {
                _scroll.RefreshCells();
                return;
            }

            if (fromEnd)
                _scroll.RefillCellsFromEnd();
            else
                _scroll.RefillCells();
        }

        public void Refresh()
        {
            EnsureOpen();
            _scroll.totalCount = _items.Count;
            _scroll.RefreshCells();
        }

        /// <summary>
        /// Grow totalCount without refill. LoopScrollRect will NewItemAtEnd on scroll.
        /// </summary>
        public void NotifyAppended()
        {
            if (_items.Count == 0)
            {
                Clear();
                return;
            }

            _scroll.totalCount = _items.Count;
        }

        public void Clear()
        {
            if (_disposed)
                return;
            _items = Array.Empty<TItem>();
            _itemSizes.Clear();
            _scroll.ClearCells();
            _scroll.totalCount = 0;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            Clear();
            _disposed = true;

            foreach (var cell in _cells.Values)
            {
                if (cell is IDisposable disposable)
                    disposable.Dispose();
            }
            _cells.Clear();
            _itemSizes.Clear();

            while (_pool.Count > 0)
            {
                var go = _pool.Pop();
                if (go != null)
                    UnityEngine.Object.Destroy(go);
            }
        }

        public bool CanScroll =>
            !_disposed && _scroll != null && _scroll.isActiveAndEnabled && _scroll.gameObject.activeInHierarchy;

        public void ScrollToIndex(int index, float time = 0.2f)
        {
            EnsureOpen();
            if (_items.Count == 0 || !CanScroll)
                return;
            index = Mathf.Clamp(index, 0, _items.Count - 1);
            if (time <= 0f)
                _scroll.ScrollToCell(index, 10000f);
            else
                _scroll.ScrollToCellWithinTime(index, time);
        }

        public void ScrollToEnd(float time = 0.2f)
        {
            EnsureOpen();
            if (_items.Count == 0)
                return;
            ScrollToIndex(_items.Count - 1, time);
        }

        public void OffsetFromEnd(float pixels)
        {
            if (_scroll.content == null || pixels <= 0f)
                return;
            var pos = _scroll.content.anchoredPosition;
            if (_scroll.vertical)
                pos.y -= pixels;
            else
                pos.x += pixels;
            _scroll.content.anchoredPosition = pos;
        }

        public float EstimateLastCellSize()
        {
            var content = _scroll.content;
            if (content == null || content.childCount == 0)
                return 80f;
            var last = content.GetChild(content.childCount - 1) as RectTransform;
            if (last == null)
                return 80f;
            var size = _scroll.vertical ? last.rect.height : last.rect.width;
            return size > 1f ? size : 80f;
        }

        public GameObject GetObject(int index)
        {
            EnsureOpen();
            if (_pool.Count > 0)
            {
                var pooled = _pool.Pop();
                pooled.SetActive(true);
                return pooled;
            }

            var go = UnityEngine.Object.Instantiate(_cellPrefab);
            go.SetActive(true);
            return go;
        }

        public void ReturnObject(Transform trans)
        {
            if (trans == null)
                return;

            var go = trans.gameObject;
            if (_cells.TryGetValue(go.GetInstanceID(), out var cell) && cell is IResettableLoopScrollCellView resettable)
                resettable.Clear();
            go.SetActive(false);
            trans.SetParent(_poolRoot, false);
            _pool.Push(go);
        }

        public void ProvideData(Transform transform, int idx)
        {
            EnsureOpen();
            if (transform == null)
                throw new ArgumentNullException(nameof(transform));
            if (idx < 0 || idx >= _items.Count)
                throw new ArgumentOutOfRangeException(nameof(idx), idx, $"Item index out of range [0, {_items.Count}).");

            var id = transform.gameObject.GetInstanceID();
            if (!_cells.TryGetValue(id, out var cell) || cell == null)
            {
                cell = _cellFactory(transform.gameObject)
                       ?? throw new InvalidOperationException(
                           $"Loop scroll cell factory returned null for '{transform.name}'.");
                _cells[id] = cell;
            }

            if (cell is IResettableLoopScrollCellView resettable)
                resettable.Clear();
            cell.Bind(_items[idx]);

            var rt = transform as RectTransform;
            if (rt != null)
            {
                var h = LayoutUtility.GetPreferredHeight(rt);
                if (h > 1f)
                    _itemSizes[idx] = h;
            }
        }

        public float GetItemsSize(int itemStart, int itemEnd)
        {
            float sum = 0;
            for (var i = itemStart; i < itemEnd; i++)
                sum += _itemSizes.TryGetValue(i, out var s) && s > 1f ? s : _defaultSize;
            return sum;
        }

        private void EnsureOpen()
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().FullName);
        }
    }
}
