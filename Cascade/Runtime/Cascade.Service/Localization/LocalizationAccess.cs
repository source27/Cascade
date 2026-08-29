using System;

namespace Cascade.Service
{
    // Process-level port for AOT presentation widgets. Not a business PageContext.
    public static class LocalizationAccess
    {
        private static ILocalizationService _current;

        public static ILocalizationService Current => _current;

        public static bool IsBound => _current != null;

        public static void Bind(ILocalizationService localization)
        {
            _current = localization ?? throw new ArgumentNullException(nameof(localization));
        }

        public static void Unbind(ILocalizationService localization = null)
        {
            if (localization != null && !ReferenceEquals(_current, localization))
                return;
            _current = null;
        }

        public static bool TryGet(out ILocalizationService localization)
        {
            localization = _current;
            return localization != null;
        }

        public static string Get(string key)
        {
            return _current != null ? _current.Get(key) : key ?? string.Empty;
        }
    }
}
