using System.Threading;
using Cascade.Core;
using Cascade.Service;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Cascade.Indie
{
    public static class GameEntry
    {
        public static async UniTask Start(IGameHost host, CancellationToken cancellationToken = default)
        {
            var bytes = await host.Resources.LoadRawBytesAsync("localization_catalog", cancellationToken);
            host.Log.Info("Indie", $"Loaded localization_catalog ({bytes.Length} bytes).");

            var canvasGo = new GameObject("IndieSmokeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Object.DontDestroyOnLoad(canvasGo);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(canvasGo.transform, false);
            var rt = textGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var text = textGo.GetComponent<Text>();
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 36;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var locale = host.Localization != null ? host.Localization.CurrentLocale : "?";
            text.text = $"Cascade Indie Starter\nlocale={locale}\nAddressables OK";

            host.Log.Info("Indie", "GameEntry ready.");
            await UniTask.CompletedTask;
        }
    }
}
