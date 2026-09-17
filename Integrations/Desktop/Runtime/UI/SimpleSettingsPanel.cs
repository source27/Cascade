using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Cascade.Integrations.Desktop
{
    /// <summary>
    /// 最小设置面板（运行时 uGUI，无 TMP）：绑定 <see cref="GameSettingsService"/>。
    /// UI 标签明确区分 <b>Local</b>（显示/画质）与 <b>Roaming</b>（音量等）。
    /// </summary>
    public sealed class SimpleSettingsPanel : MonoBehaviour
    {
        static readonly Vector2Int[] CommonResolutions =
        {
            new Vector2Int(1280, 720),
            new Vector2Int(1600, 900),
            new Vector2Int(1920, 1080),
            new Vector2Int(2560, 1440),
            new Vector2Int(3840, 2160)
        };

        GameSettingsService _service;
        Dropdown _resolution;
        Toggle _fullscreen;
        Dropdown _quality;
        Toggle _vSync;
        InputField _frameCap;
        Slider _master, _bgm, _sfx;
        GameObject _root;

        public static SimpleSettingsPanel Show(GameSettingsService service)
        {
            var canvas = DesktopModalCanvas.Ensure(5200);
            var existing = canvas.GetComponentInChildren<SimpleSettingsPanel>(true);
            if (existing != null)
            {
                existing._service = service ?? existing._service;
                existing.RefreshFromService();
                existing.gameObject.SetActive(true);
                return existing;
            }

            var go = new GameObject("SimpleSettingsPanel");
            go.transform.SetParent(canvas.transform, false);
            var panel = go.AddComponent<SimpleSettingsPanel>();
            panel._service = service ?? new GameSettingsService();
            panel.Build();
            panel.RefreshFromService();
            return panel;
        }

        public void Hide() => gameObject.SetActive(false);

        void Build()
        {
            var blocker = new GameObject("Blocker");
            blocker.transform.SetParent(transform, false);
            var bImg = blocker.AddComponent<Image>();
            bImg.color = new Color(0f, 0f, 0f, 0.5f);
            Stretch(blocker.GetComponent<RectTransform>());

            _root = new GameObject("Panel");
            _root.transform.SetParent(transform, false);
            var pImg = _root.AddComponent<Image>();
            pImg.color = new Color(0.1f, 0.1f, 0.12f, 0.98f);
            var prt = _root.GetComponent<RectTransform>();
            prt.sizeDelta = new Vector2(520f, 640f);

            float y = 290f;
            AddLabel(_root.transform, "=== LOCAL (this machine only) ===", ref y, 18, new Color(1f, 0.85f, 0.4f));
            _resolution = AddDropdown(_root.transform, "Resolution", ref y, BuildResolutionOptions());
            _fullscreen = AddToggle(_root.transform, "Fullscreen (borderless)", ref y);
            _quality = AddDropdown(_root.transform, "Quality", ref y, BuildQualityOptions());
            _vSync = AddToggle(_root.transform, "vSync", ref y);
            _frameCap = AddInput(_root.transform, "Frame cap", ref y);

            y -= 12f;
            AddLabel(_root.transform, "=== ROAMING (cloud-eligible) ===", ref y, 18, new Color(0.5f, 0.9f, 1f));
            _master = AddSlider(_root.transform, "Master volume", ref y);
            _bgm = AddSlider(_root.transform, "BGM volume", ref y);
            _sfx = AddSlider(_root.transform, "SFX volume", ref y);

            float btnY = -290f;
            float bx = -165f;
            AddActionButton(_root.transform, "Apply", new Vector2(bx, btnY), OnApply); bx += 110f;
            AddActionButton(_root.transform, "Save", new Vector2(bx, btnY), OnSave); bx += 110f;
            AddActionButton(_root.transform, "Reset", new Vector2(bx, btnY), OnReset); bx += 110f;
            AddActionButton(_root.transform, "Close", new Vector2(bx, btnY), Hide);
        }

        void RefreshFromService()
        {
            if (_service == null) return;
            if (_service.Current == null)
                _service.Load();
            var m = _service.Current;
            if (m == null) return;

            int resIdx = 0;
            for (int i = 0; i < CommonResolutions.Length; i++)
            {
                if (CommonResolutions[i].x == m.resolutionWidth && CommonResolutions[i].y == m.resolutionHeight)
                {
                    resIdx = i;
                    break;
                }
            }
            if (_resolution != null) _resolution.value = resIdx;
            if (_fullscreen != null) _fullscreen.isOn = m.fullscreenMode != 3;
            if (_quality != null)
            {
                int qMax = Mathf.Max(0, (_quality.options?.Count ?? 1) - 1);
                _quality.value = Mathf.Clamp(m.qualityLevel, 0, qMax);
            }
            if (_vSync != null) _vSync.isOn = m.vSync;
            if (_frameCap != null) _frameCap.text = m.targetFrameRate.ToString();
            if (_master != null) _master.value = m.masterVolume;
            if (_bgm != null) _bgm.value = m.bgmVolume;
            if (_sfx != null) _sfx.value = m.sfxVolume;
        }

        void PushToModel()
        {
            if (_service?.Current == null) return;
            var m = _service.Current;
            if (_resolution != null)
            {
                var r = CommonResolutions[Mathf.Clamp(_resolution.value, 0, CommonResolutions.Length - 1)];
                m.resolutionWidth = r.x;
                m.resolutionHeight = r.y;
            }
            if (_fullscreen != null)
                m.fullscreenMode = _fullscreen.isOn ? 1 : 3;
            if (_quality != null)
                m.qualityLevel = _quality.value;
            if (_vSync != null)
                m.vSync = _vSync.isOn;
            if (_frameCap != null && int.TryParse(_frameCap.text, out var fps))
                m.targetFrameRate = Mathf.Clamp(fps, 0, 1000);
            if (_master != null) m.masterVolume = _master.value;
            if (_bgm != null) m.bgmVolume = _bgm.value;
            if (_sfx != null) m.sfxVolume = _sfx.value;
        }

        void OnApply()
        {
            PushToModel();
            _service?.Apply();
        }

        void OnSave()
        {
            PushToModel();
            _service?.Apply();
            _service?.Save();
        }

        void OnReset()
        {
            _service?.ResetToDefaults();
            RefreshFromService();
            _service?.Apply();
        }

        static List<Dropdown.OptionData> BuildResolutionOptions()
        {
            var list = new List<Dropdown.OptionData>();
            foreach (var r in CommonResolutions)
                list.Add(new Dropdown.OptionData($"{r.x}x{r.y}"));
            return list;
        }

        static List<Dropdown.OptionData> BuildQualityOptions()
        {
            var names = QualitySettings.names;
            var list = new List<Dropdown.OptionData>();
            if (names == null || names.Length == 0)
            {
                list.Add(new Dropdown.OptionData("Default"));
                return list;
            }
            foreach (var n in names)
                list.Add(new Dropdown.OptionData(n));
            return list;
        }

        static Font UiFont() => Resources.GetBuiltinResource<Font>("Arial.ttf");

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void AddLabel(Transform parent, string text, ref float y, int size, Color color)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = UiFont();
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAnchor.MiddleLeft;
            t.text = text;
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(480f, 28f);
            rt.anchoredPosition = new Vector2(0f, y);
            y -= 32f;
        }

        static Dropdown AddDropdown(Transform parent, string label, ref float y, List<Dropdown.OptionData> options)
        {
            AddLabel(parent, label, ref y, 14, Color.white);
            var go = new GameObject(label + "Dropdown");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.22f);
            var dd = go.AddComponent<Dropdown>();
            dd.targetGraphic = img;
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            var lt = labelGo.AddComponent<Text>();
            lt.font = UiFont();
            lt.fontSize = 16;
            lt.color = Color.white;
            lt.alignment = TextAnchor.MiddleLeft;
            Stretch(lt.rectTransform);
            lt.rectTransform.offsetMin = new Vector2(8, 0);
            dd.captionText = lt;

            var template = new GameObject("Template");
            template.transform.SetParent(go.transform, false);
            template.SetActive(false);
            var tImg = template.AddComponent<Image>();
            tImg.color = new Color(0.15f, 0.15f, 0.18f);
            var scroll = template.AddComponent<ScrollRect>();
            var tRt = template.GetComponent<RectTransform>();
            tRt.anchorMin = new Vector2(0, 0);
            tRt.anchorMax = new Vector2(1, 0);
            tRt.pivot = new Vector2(0.5f, 1f);
            tRt.sizeDelta = new Vector2(0, 120f);

            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(template.transform, false);
            viewport.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.18f);
            viewport.AddComponent<Mask>().showMaskGraphic = false;
            Stretch(viewport.GetComponent<RectTransform>());

            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            var cRt = content.AddComponent<RectTransform>();
            cRt.anchorMin = new Vector2(0, 1);
            cRt.anchorMax = new Vector2(1, 1);
            cRt.pivot = new Vector2(0.5f, 1);
            cRt.sizeDelta = new Vector2(0, 28);

            var item = new GameObject("Item");
            item.transform.SetParent(content.transform, false);
            var toggle = item.AddComponent<Toggle>();
            var itemBg = item.AddComponent<Image>();
            itemBg.color = new Color(0.2f, 0.2f, 0.25f);
            toggle.targetGraphic = itemBg;
            var itemRt = item.GetComponent<RectTransform>();
            itemRt.anchorMin = new Vector2(0, 0.5f);
            itemRt.anchorMax = new Vector2(1, 0.5f);
            itemRt.sizeDelta = new Vector2(0, 28);

            var itemLabel = new GameObject("Item Label");
            itemLabel.transform.SetParent(item.transform, false);
            var il = itemLabel.AddComponent<Text>();
            il.font = UiFont();
            il.fontSize = 16;
            il.color = Color.white;
            Stretch(il.rectTransform);
            il.rectTransform.offsetMin = new Vector2(8, 0);
            toggle.graphic = null;

            scroll.content = cRt;
            scroll.viewport = viewport.GetComponent<RectTransform>();
            dd.template = tRt;
            dd.itemText = il;
            dd.options = options ?? new List<Dropdown.OptionData>();

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(400f, 32f);
            rt.anchoredPosition = new Vector2(0f, y);
            y -= 40f;
            return dd;
        }

        static Toggle AddToggle(Transform parent, string label, ref float y)
        {
            var go = new GameObject(label + "Toggle");
            go.transform.SetParent(parent, false);
            var bg = new GameObject("Background");
            bg.transform.SetParent(go.transform, false);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.25f, 0.25f, 0.3f);
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.sizeDelta = new Vector2(24f, 24f);
            bgRt.anchoredPosition = new Vector2(-190f, 0f);

            var check = new GameObject("Checkmark");
            check.transform.SetParent(bg.transform, false);
            var cImg = check.AddComponent<Image>();
            cImg.color = new Color(0.4f, 0.9f, 0.5f);
            Stretch(check.GetComponent<RectTransform>());

            var toggle = go.AddComponent<Toggle>();
            toggle.targetGraphic = bgImg;
            toggle.graphic = cImg;

            var tGo = new GameObject("Label");
            tGo.transform.SetParent(go.transform, false);
            var t = tGo.AddComponent<Text>();
            t.font = UiFont();
            t.fontSize = 16;
            t.color = Color.white;
            t.text = label;
            var trt = t.rectTransform;
            trt.sizeDelta = new Vector2(340f, 28f);
            trt.anchoredPosition = new Vector2(40f, 0f);

            var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(400f, 28f);
            rt.anchoredPosition = new Vector2(0f, y);
            y -= 36f;
            return toggle;
        }

        static InputField AddInput(Transform parent, string label, ref float y)
        {
            AddLabel(parent, label, ref y, 14, Color.white);
            var go = new GameObject(label + "Input");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.22f);
            var input = go.AddComponent<InputField>();
            var tGo = new GameObject("Text");
            tGo.transform.SetParent(go.transform, false);
            var t = tGo.AddComponent<Text>();
            t.font = UiFont();
            t.fontSize = 16;
            t.color = Color.white;
            t.supportRichText = false;
            Stretch(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(8, 0);
            input.textComponent = t;
            input.contentType = InputField.ContentType.IntegerNumber;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(400f, 32f);
            rt.anchoredPosition = new Vector2(0f, y);
            y -= 40f;
            return input;
        }

        static Slider AddSlider(Transform parent, string label, ref float y)
        {
            AddLabel(parent, label, ref y, 14, Color.white);
            var go = new GameObject(label + "Slider");
            go.transform.SetParent(parent, false);
            var slider = go.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;

            var bg = new GameObject("Background");
            bg.transform.SetParent(go.transform, false);
            bg.AddComponent<Image>().color = new Color(0.25f, 0.25f, 0.3f);
            Stretch(bg.GetComponent<RectTransform>());

            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(go.transform, false);
            var faRt = fillArea.AddComponent<RectTransform>();
            Stretch(faRt);
            faRt.offsetMin = new Vector2(5, 0);
            faRt.offsetMax = new Vector2(-5, 0);
            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var fImg = fill.AddComponent<Image>();
            fImg.color = new Color(0.3f, 0.6f, 0.95f);
            Stretch(fill.GetComponent<RectTransform>());
            slider.fillRect = fill.GetComponent<RectTransform>();

            var handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(go.transform, false);
            Stretch(handleArea.AddComponent<RectTransform>());
            var handle = new GameObject("Handle");
            handle.transform.SetParent(handleArea.transform, false);
            var hImg = handle.AddComponent<Image>();
            hImg.color = Color.white;
            var hRt = handle.GetComponent<RectTransform>();
            hRt.sizeDelta = new Vector2(16f, 0f);
            slider.handleRect = hRt;
            slider.targetGraphic = hImg;
            slider.direction = Slider.Direction.LeftToRight;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(400f, 24f);
            rt.anchoredPosition = new Vector2(0f, y);
            y -= 40f;
            return slider;
        }

        static void AddActionButton(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label + "Btn");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.28f, 0.28f, 0.34f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(100f, 36f);
            rt.anchoredPosition = pos;
            var tGo = new GameObject("Text");
            tGo.transform.SetParent(go.transform, false);
            var t = tGo.AddComponent<Text>();
            t.font = UiFont();
            t.fontSize = 16;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.text = label;
            Stretch(t.rectTransform);
        }
    }
}
