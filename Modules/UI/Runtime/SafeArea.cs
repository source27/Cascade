using UnityEngine;
using UnityEngine.UI;

namespace Cascade.Modules.UI
{
    /// <summary>
    /// Fits a RectTransform into Screen.safeArea and optionally draws notch masks.
    /// Top/Bottom masks fill the unsafe strips in the *parent* full-screen space
    /// (not overflowing above the fitted rect), so they remain visible whenever
    /// Screen.safeArea has insets. On Editor without a notch, insets are zero —
    /// use Device Simulator to verify.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [ExecuteAlways]
    public sealed class SafeArea : MonoBehaviour
    {
        private const string TopMaskObjectName = "TopSafeAreaMask";
        private const string BottomMaskObjectName = "BottomSafeAreaMask";

        private RectTransform _rectTransform;
        private RectTransform _topMaskRectTransform;
        private RectTransform _bottomMaskRectTransform;
        private Rect _lastSafeArea;
        private int _lastScreenWidth;
        private int _lastScreenHeight;
        private UISafeAreaPolicy _lastPolicy;
        private UISafeAreaPolicy _policy;
        private Sprite _maskSprite;

        public void Configure(UISafeAreaPolicy policy, Sprite maskSprite)
        {
            _policy = policy;
            _maskSprite = maskSprite;
            if ((policy & (UISafeAreaPolicy.TopMask | UISafeAreaPolicy.BottomMask)) != 0 && maskSprite == null)
                throw new global::System.InvalidOperationException(
                    "SafeArea mask sprite is required when TopMask/BottomMask is enabled. Assign UIRoot.safeAreaMaskSprite on UIRootPrefab.");

            if (_rectTransform == null)
                _rectTransform = GetComponent<RectTransform>();
            Apply(Screen.safeArea, Screen.width, Screen.height);
            CacheState(Screen.safeArea, Screen.width, Screen.height);
        }

        private void OnEnable()
        {
            _rectTransform = GetComponent<RectTransform>();
            Apply(Screen.safeArea, Screen.width, Screen.height);
            CacheState(Screen.safeArea, Screen.width, Screen.height);
        }

        private void Update()
        {
            var safeArea = Screen.safeArea;
            var screenWidth = Screen.width;
            var screenHeight = Screen.height;
            if (_lastSafeArea == safeArea &&
                _lastScreenWidth == screenWidth &&
                _lastScreenHeight == screenHeight &&
                _lastPolicy == _policy)
                return;

            Apply(safeArea, screenWidth, screenHeight);
            CacheState(safeArea, screenWidth, screenHeight);
        }

        private void OnDestroy()
        {
            DestroyMask(ref _topMaskRectTransform);
            DestroyMask(ref _bottomMaskRectTransform);
        }

        private void CacheState(Rect safeArea, int screenWidth, int screenHeight)
        {
            _lastSafeArea = safeArea;
            _lastScreenWidth = screenWidth;
            _lastScreenHeight = screenHeight;
            _lastPolicy = _policy;
        }

        private void Apply(Rect safeArea, int screenWidth, int screenHeight)
        {
            if (_rectTransform == null || screenWidth <= 0 || screenHeight <= 0)
                return;

            var bottom = Mathf.Clamp01(safeArea.yMin / screenHeight);
            var top = Mathf.Clamp01(safeArea.yMax / screenHeight);
            if (top < bottom)
                top = bottom;

            var fitBottom = (_policy & UISafeAreaPolicy.FitBottom) != 0;
            var fitTop = (_policy & UISafeAreaPolicy.FitTop) != 0;
            var anchorMinY = fitBottom ? bottom : 0f;
            var anchorMaxY = fitTop ? top : 1f;

            _rectTransform.anchorMin = new Vector2(0f, anchorMinY);
            _rectTransform.anchorMax = new Vector2(1f, anchorMaxY);
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;

            // Masks live on the full-screen parent and fill unsafe strips.
            // Parenting to the fitted rect + overflowing upward is invisible when safeArea == full screen.
            UpdateStripMask(
                (_policy & UISafeAreaPolicy.TopMask) != 0,
                TopMaskObjectName,
                ref _topMaskRectTransform,
                anchorMin: new Vector2(0f, top),
                anchorMax: new Vector2(1f, 1f));
            UpdateStripMask(
                (_policy & UISafeAreaPolicy.BottomMask) != 0,
                BottomMaskObjectName,
                ref _bottomMaskRectTransform,
                anchorMin: new Vector2(0f, 0f),
                anchorMax: new Vector2(1f, bottom));
        }

        private void UpdateStripMask(
            bool enabled,
            string objectName,
            ref RectTransform maskRectTransform,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            if (!enabled || anchorMax.y <= anchorMin.y + 0.0001f)
            {
                DestroyMask(ref maskRectTransform);
                return;
            }

            var parent = _rectTransform.parent as RectTransform;
            if (parent == null)
            {
                DestroyMask(ref maskRectTransform);
                return;
            }

            if (maskRectTransform == null)
                maskRectTransform = CreateMask(objectName, parent);
            else if (maskRectTransform.parent != parent)
                maskRectTransform.SetParent(parent, false);

            maskRectTransform.anchorMin = anchorMin;
            maskRectTransform.anchorMax = anchorMax;
            maskRectTransform.pivot = new Vector2(0.5f, 0.5f);
            maskRectTransform.offsetMin = Vector2.zero;
            maskRectTransform.offsetMax = Vector2.zero;
            maskRectTransform.localScale = Vector3.one;
            maskRectTransform.localRotation = Quaternion.identity;
            maskRectTransform.SetAsLastSibling();

            var image = maskRectTransform.GetComponent<Image>();
            image.sprite = _maskSprite;
            image.type = _maskSprite != null && _maskSprite.border.sqrMagnitude > 0f
                ? Image.Type.Sliced
                : Image.Type.Simple;
            image.preserveAspect = false;
            image.raycastTarget = false;
            image.color = Color.white;
        }

        private RectTransform CreateMask(string objectName, Transform parent)
        {
            var maskObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var maskTransform = maskObject.GetComponent<RectTransform>();
            maskTransform.SetParent(parent, false);
            return maskTransform;
        }

        private static void DestroyMask(ref RectTransform maskRectTransform)
        {
            if (maskRectTransform == null)
                return;
            if (Application.isPlaying)
                Destroy(maskRectTransform.gameObject);
            else
                DestroyImmediate(maskRectTransform.gameObject);
            maskRectTransform = null;
        }
    }
}
