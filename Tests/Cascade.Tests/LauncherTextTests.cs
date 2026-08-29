using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Cascade.Launcher;
using NUnit.Framework;

namespace Cascade.Tests
{
    /// <summary>
    /// LauncherText 迷你词表完整性：所有 key 在所有语言都有词条；
    /// 占位符数量跨语言一致；语言清单齐全。
    /// </summary>
    public sealed class LauncherTextTests
    {
        private static readonly string[] RequiredLocales =
        {
            "zh-CN", "en", "fr", "de", "id", "pt", "ru", "es",
            "th", "tr", "ko", "ja", "ar", "zh-TW"
        };

        private static readonly string[] Keys =
        {
            LauncherText.Starting, LauncherText.Retrying, LauncherText.Cancelled,
            LauncherText.CheckInstall, LauncherText.InitResource, LauncherText.CheckVersion,
            LauncherText.LoadManifest, LauncherText.NetworkOfflineUseLocal,
            LauncherText.CountingUpdate, LauncherText.UpdateFoundStartDownload,
            LauncherText.StartDownload, LauncherText.DownloadingPercent,
            LauncherText.DownloadingSize, LauncherText.DownloadComplete, LauncherText.UpToDate,
            LauncherText.CleaningObsolete, LauncherText.EditorSimulate, LauncherText.BuiltinResource,
            LauncherText.NoAotMetadata, LauncherText.LoadingAotMetadata,
            LauncherText.LoadingLocalization, LauncherText.LoadingHotUpdate,
            LauncherText.LaunchingGame, LauncherText.Completed,
            LauncherText.ConfirmUpdate, LauncherText.WaitingConfirm,
            LauncherText.ErrorTitle, LauncherText.ErrorInitResource,
            LauncherText.ErrorNoServerNoLocal, LauncherText.ErrorLocalIncomplete,
            LauncherText.ErrorDownload, LauncherText.ErrorLoadLocalization,
            LauncherText.ErrorLoadAotMetadata, LauncherText.ErrorLoadGameCode,
            LauncherText.ErrorLaunchGame, LauncherText.ErrorResource,
            LauncherText.ErrorLoadGame, LauncherText.ErrorUnknown,
        };

        [Test]
        public void SupportedLocalesCoverAllRequiredLanguages()
        {
            var supported = new HashSet<string>(LauncherText.SupportedLocales, StringComparer.Ordinal);
            foreach (var locale in RequiredLocales)
                Assert.That(supported, Does.Contain(locale), $"缺少语言: {locale}");
        }

        [Test]
        public void EveryKeyResolvesNonEmptyTextInEveryLocale()
        {
            foreach (var locale in LauncherText.SupportedLocales)
            {
                LauncherText.Initialize(locale);
                foreach (var key in Keys)
                {
                    var text = LauncherText.Get(key);
                    Assert.That(string.IsNullOrWhiteSpace(text), Is.False,
                        $"locale={locale} key={key} 词条为空");
                    Assert.That(text, Does.Not.Contain("{{").Or.Not.Contain("}}"),
                        $"locale={locale} key={key} 疑似未替换占位符");
                }
            }
        }

        [Test]
        public void PlaceholderCountsMatchAcrossLocales()
        {
            // 基准：zh-CN 每个 key 的 {N} 占位符数量
            LauncherText.Initialize("zh-CN");
            var baseline = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var key in Keys)
                baseline[key] = CountPlaceholders(LauncherText.Get(key));

            foreach (var locale in LauncherText.SupportedLocales)
            {
                LauncherText.Initialize(locale);
                foreach (var key in Keys)
                {
                    var expected = baseline[key];
                    var actual = CountPlaceholders(LauncherText.Get(key));
                    Assert.That(actual, Is.EqualTo(expected),
                        $"locale={locale} key={key} 占位符数量不一致: 期望 {expected} 实际 {actual}");
                }
            }
        }

        [Test]
        public void FormatReplacesPlaceholders()
        {
            LauncherText.Initialize("en");
            var formatted = LauncherText.Format(LauncherText.DownloadingSize, 42, "1 MB", "5 MB");
            Assert.That(formatted, Does.Contain("42%"));
            Assert.That(formatted, Does.Contain("1 MB"));
            Assert.That(formatted, Does.Contain("5 MB"));
            Assert.That(formatted, Does.Not.Contain("{0}"));

            LauncherText.Initialize("ar");
            formatted = LauncherText.Format(LauncherText.DownloadingSize, 7, "1 MB", "5 MB");
            Assert.That(formatted, Does.Contain("7%"));
            Assert.That(formatted, Does.Not.Contain("{0}"));
        }

        [Test]
        public void InitializeFallsBackToDefaultLocale()
        {
            LauncherText.Initialize("xx-XX"); // 不支持的语言 → 回退默认 en
            Assert.That(LauncherText.CurrentLocale, Is.EqualTo("en"));

            LauncherText.Initialize("FR"); // 规范化后应命中 fr
            Assert.That(LauncherText.CurrentLocale, Is.EqualTo("fr"));
        }

        private static int CountPlaceholders(string text)
        {
            return Regex.Matches(text, @"\{\d+\}").Count;
        }
    }
}
