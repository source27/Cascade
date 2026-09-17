using System.Collections;
using Cascade.Integrations.Desktop;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cascade.Starters.Indie.DesktopShell
{
    /// <summary>
    /// 薄启动：可选停留 → <see cref="SceneFader"/> 淡出 → 加载可配置 bootstrap 场景 → 淡入。
    /// </summary>
    public sealed class IndieBootSplashFlow : MonoBehaviour
    {
        [SerializeField] string _nextSceneName = "IndieBootstrap";
        [SerializeField] float _holdSeconds = 0.75f;
        [SerializeField] bool _useFader = true;
        [SerializeField] LoadSceneMode _loadMode = LoadSceneMode.Single;

        void Start() => StartCoroutine(Run());

        IEnumerator Run()
        {
            if (_holdSeconds > 0f)
                yield return new WaitForSecondsRealtime(_holdSeconds);

            SceneFader fader = null;
            if (_useFader)
            {
                fader = SceneFader.Instance;
                if (fader == null)
                {
                    var go = new GameObject("SceneFader");
                    fader = go.AddComponent<SceneFader>();
                }
                yield return fader.FadeOut();
            }

            if (string.IsNullOrEmpty(_nextSceneName))
            {
                Debug.LogWarning("[IndieBootSplashFlow] next scene name empty.");
                yield break;
            }

            var op = SceneManager.LoadSceneAsync(_nextSceneName, _loadMode);
            if (op == null)
            {
                Debug.LogError($"[IndieBootSplashFlow] Failed to load '{_nextSceneName}'.");
                yield break;
            }

            while (!op.isDone)
                yield return null;

            if (fader != null)
                yield return fader.FadeIn();
        }
    }
}
