using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Cascade.Service
{
    public interface ILocalizationService
    {
        string CurrentLocale { get; }
        IReadOnlyList<string> SupportedLocales { get; }
        event Action<string> LocaleChanged;

        UniTask InitializeAsync(CancellationToken cancellationToken = default);
        UniTask SetLocaleAsync(string locale, CancellationToken cancellationToken = default);
        string Get(string key);
    }
}
