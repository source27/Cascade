# Cascade Indie Starter

Desktop/console starter: Cascade + Addressables. No HybridCLR, YooAsset, or patch window.

Unity **2022.3.62f3**. Open `Starters/Indie`.

## First-time Addressables setup

1. Window → Asset Management → Addressables → Groups (create defaults if asked).
2. Mark `Assets/AddressableContent/localization_catalog.json` with address `localization_catalog`.
3. Mark `loc_en.json` / `loc_zh.json` as `loc_en` / `loc_zh`.
4. Open `Assets/Scenes/Bootstrap.unity` and Play.

Optional authoring: package `com.source27.cascade.modules.localizationtools` enables **Cascade/多语言设置** and **Cascade/更新多语言** (Sheet → runtime JSON). JSON-only workflows can omit it.

DoD: Bootstrap pipeline → Addressables load → localization init → minimal UI label.
