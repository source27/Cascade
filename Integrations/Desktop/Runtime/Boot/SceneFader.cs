using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Cascade.Integrations.Desktop
{
    /// <summary>
    /// CanvasGroup 淡入淡出辅助（Coroutine，无 UniTask 依赖）。
    /// 可选自建全黑 Overlay；场景加载逻辑由游戏侧驱动。
    /// </summary>
    public sealed class SceneFader : MonoBehaviour
    {
        public static SceneFader Instance { get; private set; }

        [SerializeField] CanvasGroup _group;
        [SerializeField] float _fadeSeconds = 0.35f;
        [SerializeField] bool _dontDestroyOnLoad = true;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (_dontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);

            EnsureUi();
            if (_group != null)
            {
                _group.alpha = 0f;
                _group.blocksRaycasts = false;
            }
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void EnsureUi()
        {
            if (_group != null)
                return;

            var canvasGo = new GameObject("FaderCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            var imgGo = new GameObject("Fade");
            imgGo.transform.SetParent(canvasGo.transform, false);
            var img = imgGo.AddComponent<Image>();
            img.color = Color.black;
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            _group = imgGo.AddComponent<CanvasGroup>();
        }

        public Coroutine FadeOut() => StartCoroutine(Fade(0f, 1f));
        public Coroutine FadeIn() => StartCoroutine(Fade(1f, 0f));

        public IEnumerator Fade(float from, float to)
        {
            EnsureUi();
            if (_group == null)
                yield break;

            _group.blocksRaycasts = true;
            float t = 0f;
            float dur = Mathf.Max(0.01f, _fadeSeconds);
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                _group.alpha = Mathf.Lerp(from, to, t / dur);
                yield return null;
            }

            _group.alpha = to;
            _group.blocksRaycasts = to > 0.9f;
        }

        public void SetAlpha(float alpha)
        {
            EnsureUi();
            if (_group == null)
                return;
            _group.alpha = Mathf.Clamp01(alpha);
            _group.blocksRaycasts = _group.alpha > 0.9f;
        }
    }
}
