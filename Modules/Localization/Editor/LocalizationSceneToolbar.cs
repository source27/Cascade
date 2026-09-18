using System;
using Cysharp.Threading.Tasks;
using Cascade.Service;
using Cascade.Modules.Localization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Cascade.Modules.Localization.Editor
{
    [InitializeOnLoad]
    public static class LocalizationSceneToolbar
    {
        private static readonly EditorLocalizationPreview Preview = new EditorLocalizationPreview();
        private static string[] _locales = Array.Empty<string>();
        private static int _selectedIndex;
        private static bool _boundToPreview;
        private static string _pendingPlayLocale;

        static LocalizationSceneToolbar()
        {
            EditorApplication.delayCall += Bootstrap;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            PrefabStage.prefabStageOpened += _ => EditorApplication.delayCall += RefreshAfterStageChange;
            PrefabStage.prefabStageClosing += _ => EditorApplication.delayCall += RefreshAfterStageChange;
            SceneView.duringSceneGui += OnSceneGui;
        }

        private static void Bootstrap()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            ActivatePreview(reload: true);
        }

        private static void RefreshAfterStageChange()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (!_boundToPreview)
                ActivatePreview(reload: false);
            else
                RefreshLocalizedTexts();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    _pendingPlayLocale = null;
                    DeactivatePreview();
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    _pendingPlayLocale = null;
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    ActivatePreview(reload: true);
                    break;
            }
        }

        private static void OnSceneGui(SceneView sceneView)
        {
            if (EditorApplication.isPlaying)
            {
                DrawPlayModePopup(sceneView);
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (!Preview.IsLoaded || _locales.Length == 0)
                return;

            Handles.BeginGUI();
            var width = 120f;
            var rect = new Rect(sceneView.position.width - width - 120f, 8f, width, 20f);
            var next = EditorGUI.Popup(rect, _selectedIndex, _locales);
            if (next != _selectedIndex && next >= 0 && next < _locales.Length)
            {
                _selectedIndex = next;
                var locale = _locales[_selectedIndex];
                EditorApplication.delayCall += () =>
                {
                    if (EditorApplication.isPlayingOrWillChangePlaymode)
                        return;
                    Preview.SetLocale(locale);
                    RefreshLocalizedTexts();
                };
            }
            Handles.EndGUI();
        }

        private static void DrawPlayModePopup(SceneView sceneView)
        {
            if (!TryGetPlayModeLocalization(out var localization))
                return;

            SyncPlayModeLocales(localization);
            if (_locales.Length == 0)
                return;

            Handles.BeginGUI();
            var width = 120f;
            var rect = new Rect(sceneView.position.width - width - 120f, 8f, width, 20f);
            var next = EditorGUI.Popup(rect, _selectedIndex, _locales);
            if (next != _selectedIndex && next >= 0 && next < _locales.Length)
            {
                _selectedIndex = next;
                ApplyPlayModeLocale(localization, _locales[_selectedIndex]).Forget();
            }
            Handles.EndGUI();
        }

        private static bool TryGetPlayModeLocalization(out ILocalizationService localization)
        {
            localization = null;
            if (!LocalizationAccess.TryGet(out var current) || current is EditorLocalizationPreview)
                return false;
            localization = current;
            return true;
        }

        private static void SyncPlayModeLocales(ILocalizationService localization)
        {
            var supported = localization.SupportedLocales;
            var count = supported != null ? supported.Count : 0;
            if (_locales.Length != count)
                _locales = new string[count];
            for (var i = 0; i < count; i++)
                _locales[i] = supported[i];

            if (_pendingPlayLocale != null)
            {
                if (string.Equals(localization.CurrentLocale, _pendingPlayLocale, StringComparison.OrdinalIgnoreCase))
                    _pendingPlayLocale = null;
                else
                    return;
            }

            _selectedIndex = Math.Max(0, Array.FindIndex(
                _locales,
                item => string.Equals(item, localization.CurrentLocale, StringComparison.OrdinalIgnoreCase)));
        }

        private static async UniTaskVoid ApplyPlayModeLocale(ILocalizationService localization, string locale)
        {
            _pendingPlayLocale = locale;
            try
            {
                await localization.SetLocaleAsync(locale);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                if (string.Equals(_pendingPlayLocale, locale, StringComparison.OrdinalIgnoreCase))
                    _pendingPlayLocale = null;
            }
        }

        private static void ActivatePreview(bool reload)
        {
            if (reload && !Preview.Reload())
            {
                DeactivatePreview();
                return;
            }

            if (!Preview.IsLoaded)
                return;

            LocalizationAccess.Bind(Preview);
            _boundToPreview = true;
            _locales = new string[Preview.DropdownOptions.Count];
            for (var i = 0; i < Preview.DropdownOptions.Count; i++)
                _locales[i] = Preview.DropdownOptions[i];
            _selectedIndex = Math.Max(0, Array.FindIndex(
                _locales,
                item => string.Equals(item, Preview.CurrentLocale, StringComparison.OrdinalIgnoreCase)));
            RefreshLocalizedTexts();
        }

        private static void DeactivatePreview()
        {
            if (_boundToPreview)
            {
                LocalizationAccess.Unbind(Preview);
                _boundToPreview = false;
            }
            RefreshLocalizedTexts();
        }

        private static void RefreshLocalizedTexts()
        {
            var texts = Resources.FindObjectsOfTypeAll<LocalizedText>();
            for (var i = 0; i < texts.Length; i++)
            {
                var text = texts[i];
                if (!CanPreview(text))
                    continue;
                text.RebindAccess();
                EditorUtility.SetDirty(text);
                foreach (var graphic in text.GetComponents<Graphic>())
                    EditorUtility.SetDirty(graphic);
            }

            Canvas.ForceUpdateCanvases();
            SceneView.RepaintAll();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        private static bool CanPreview(LocalizedText text)
        {
            if (text == null)
                return false;
            // Scene objects and Prefab Stage contents have a valid scene.
            // Skip pure project assets so we don't bake preview strings into prefab files.
            return text.gameObject.scene.IsValid();
        }

        private sealed class LocalizationAssetWatcher : AssetPostprocessor
        {
            private static void OnPostprocessAllAssets(
                string[] importedAssets,
                string[] deletedAssets,
                string[] movedAssets,
                string[] movedFromAssetPaths)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    return;
                if (!ContainsLocalizationAsset(importedAssets) &&
                    !ContainsLocalizationAsset(deletedAssets) &&
                    !ContainsLocalizationAsset(movedAssets) &&
                    !ContainsLocalizationAsset(movedFromAssetPaths))
                    return;
                ActivatePreview(reload: true);
            }

            private static bool ContainsLocalizationAsset(string[] paths)
            {
                if (paths == null)
                    return false;
                for (var i = 0; i < paths.Length; i++)
                {
                    if (EditorLocalizationPreview.IsLocalizationJsonPath(paths[i]))
                        return true;
                }
                return false;
            }
        }
    }
}
