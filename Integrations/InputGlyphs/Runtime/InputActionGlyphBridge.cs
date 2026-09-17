using System;
using System.Collections.Generic;
using InputGlyphs;
using InputGlyphs.Display;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cascade.Integrations.InputGlyphs
{
    /// <summary>
    /// 游戏无关的 InputAction → Glyph 桥接（自 d1 GameInputGlyphBridge 抽离）。
    /// 实现 <see cref="IInputGlyphService"/>：按当前 control scheme 解析绑定并生成 Sprite / 写入 Texture。
    /// </summary>
    public sealed class InputActionGlyphBridge : IInputGlyphService
    {
        readonly InputActionAsset _actions;
        readonly Func<string> _schemeProvider;
        readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        readonly List<string> _pathBuffer = new List<string>(8);
        bool _disposed;

        public InputActionGlyphBridge(InputActionAsset actions, Func<string> schemeProvider)
        {
            _actions = actions ? actions : throw new ArgumentNullException(nameof(actions));
            _schemeProvider = schemeProvider ?? throw new ArgumentNullException(nameof(schemeProvider));
        }

        public bool TryCreateSprite(string actionName, out Sprite sprite)
        {
            sprite = null;
            if (_disposed || string.IsNullOrEmpty(actionName))
                return false;

            var cacheKey = CacheKey(actionName);
            if (_spriteCache.TryGetValue(cacheKey, out sprite) && sprite != null)
                return true;

            if (!TryWriteTexture(actionName, out var tex) || tex == null)
                return false;

            sprite = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f);
            sprite.name = "Glyph_" + cacheKey;
            _spriteCache[cacheKey] = sprite;
            return true;
        }

        public bool TryWriteTexture(string actionName, Texture2D destination)
        {
            if (_disposed || destination == null || string.IsNullOrEmpty(actionName))
                return false;

            if (!TryCollectPaths(actionName, out var devices))
                return false;

            var layout = new GlyphsLayoutData
            {
                Layout = GlyphsLayout.Horizontal,
                MaxCount = 4,
                Index = 0
            };
            return DisplayGlyphTextureGenerator.GenerateGlyphTexture(destination, devices, _pathBuffer, layout);
        }

        bool TryWriteTexture(string actionName, out Texture2D texture)
        {
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (TryWriteTexture(actionName, texture))
                return true;
            UnityEngine.Object.Destroy(texture);
            texture = null;
            return false;
        }

        bool TryCollectPaths(string actionName, out InputDevice[] devices)
        {
            devices = Array.Empty<InputDevice>();
            _pathBuffer.Clear();

            var action = FindAction(actionName);
            if (action == null)
                return false;

            var scheme = _schemeProvider?.Invoke();
            var bindings = action.bindings;
            for (var i = 0; i < bindings.Count; i++)
            {
                var b = bindings[i];
                if (b.isComposite)
                    continue;
                if (!string.IsNullOrEmpty(scheme) && !string.IsNullOrEmpty(b.groups))
                {
                    if (b.groups.IndexOf(scheme, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                }

                var path = b.effectivePath;
                if (string.IsNullOrEmpty(path))
                    path = b.path;
                if (!string.IsNullOrEmpty(path))
                    _pathBuffer.Add(path);
            }

            if (_pathBuffer.Count == 0)
                return false;

            var deviceList = new List<InputDevice>(InputSystem.devices.Count);
            foreach (var d in InputSystem.devices)
                deviceList.Add(d);
            devices = deviceList.ToArray();
            return devices.Length > 0;
        }

        InputAction FindAction(string actionName)
        {
            // "Map/Action" or bare action name
            var slash = actionName.IndexOf('/');
            if (slash > 0)
            {
                var mapName = actionName.Substring(0, slash);
                var name = actionName.Substring(slash + 1);
                var map = _actions.FindActionMap(mapName, throwIfNotFound: false);
                return map?.FindAction(name, throwIfNotFound: false);
            }

            return _actions.FindAction(actionName, throwIfNotFound: false);
        }

        string CacheKey(string actionName)
        {
            var scheme = _schemeProvider?.Invoke() ?? string.Empty;
            return scheme + "|" + actionName;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            foreach (var kv in _spriteCache)
            {
                if (kv.Value != null)
                {
                    if (kv.Value.texture != null)
                        UnityEngine.Object.Destroy(kv.Value.texture);
                    UnityEngine.Object.Destroy(kv.Value);
                }
            }

            _spriteCache.Clear();
        }
    }
}
