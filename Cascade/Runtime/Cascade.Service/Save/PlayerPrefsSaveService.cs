using UnityEngine;

namespace Cascade.Service
{
    public sealed class PlayerPrefsSaveService : ISaveService
    {
        public const string DefaultKeyPrefix = "f13.";

        private readonly string _keyPrefix;

        public PlayerPrefsSaveService(string keyPrefix = DefaultKeyPrefix)
        {
            _keyPrefix = keyPrefix ?? string.Empty;
        }

        public string GetString(string key, string defaultValue = "")
        {
            return PlayerPrefs.GetString(Prefixed(key), defaultValue ?? string.Empty);
        }

        public void SetString(string key, string value)
        {
            PlayerPrefs.SetString(Prefixed(key), value ?? string.Empty);
        }

        public void Flush()
        {
            PlayerPrefs.Save();
        }

        private string Prefixed(string key)
        {
            if (string.IsNullOrEmpty(key))
                throw new global::System.ArgumentException("Save key is required.", nameof(key));
            return _keyPrefix + key;
        }
    }
}
