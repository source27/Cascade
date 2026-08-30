using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Cascade.Indie.Editor
{
    /// <summary>
    /// Ensures starter localization TextAssets keep stable Addressables addresses.
    /// Runs once after domain reload so Play works without manual Groups wiring.
    /// </summary>
    [InitializeOnLoad]
    internal static class IndieAddressablesSetup
    {
        private static readonly (string Path, string Address)[] Content =
        {
            ("Assets/AddressableContent/localization_catalog.json", "localization_catalog"),
            ("Assets/AddressableContent/loc_en.json", "loc_en"),
            ("Assets/AddressableContent/loc_zh.json", "loc_zh"),
        };

        static IndieAddressablesSetup()
        {
            EditorApplication.delayCall += Ensure;
        }

        [MenuItem("Cascade/Indie/Ensure Addressables Content")]
        private static void EnsureMenu() => Ensure(forceLog: true);

        private static void Ensure() => Ensure(forceLog: false);

        private static void Ensure(bool forceLog)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                if (forceLog)
                    Debug.LogWarning("[Indie] AddressableAssetSettings missing. Open Window → Asset Management → Addressables → Groups once.");
                return;
            }

            var group = settings.DefaultGroup;
            if (group == null)
            {
                if (forceLog)
                    Debug.LogWarning("[Indie] Addressables DefaultGroup is null.");
                return;
            }

            var dirty = false;
            foreach (var (path, address) in Content)
            {
                var guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(guid))
                {
                    Debug.LogWarning($"[Indie] Missing Addressable content asset: {path}");
                    continue;
                }

                var entry = settings.FindAssetEntry(guid);
                if (entry == null)
                {
                    entry = settings.CreateOrMoveEntry(guid, group, readOnly: false, postEvent: false);
                    dirty = true;
                }

                if (entry == null)
                    continue;

                if (entry.parentGroup != group)
                {
                    settings.CreateOrMoveEntry(guid, group, readOnly: false, postEvent: false);
                    entry = settings.FindAssetEntry(guid);
                    dirty = true;
                }

                if (entry != null && entry.address != address)
                {
                    entry.SetAddress(address, postEvent: false);
                    dirty = true;
                }
            }

            if (!dirty)
            {
                if (forceLog)
                    Debug.Log("[Indie] Addressables content addresses already correct.");
                return;
            }

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, postEvent: true, settingsModified: true);
            AssetDatabase.SaveAssets();
            Debug.Log("[Indie] Addressables content addresses ensured (localization_catalog / loc_en / loc_zh).");
        }
    }
}
