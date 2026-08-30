# Cascade Indie Starter

Desktop/console starter: Cascade + Addressables. No HybridCLR, YooAsset, or patch window.

Unity **2022.3.62f3**. Open `Starters/Indie`.

## Run

1. Open `Assets/Scenes/Bootstrap.unity` and Play.
2. DoD: Bootstrap pipeline → Addressables load → localization init → minimal UI label.

Addressable addresses are pre-wired in `Assets/AddressableAssetsData`:

JSON under `AddressableContent/` must import as **TextAsset** (`TextScriptImporter` in `.meta`). `LoadRawBytesAsync` loads them as `TextAsset`.

| Asset | Address |
| --- | --- |
| `AddressableContent/localization_catalog.json` | `localization_catalog` |
| `AddressableContent/loc_en.json` | `loc_en` |
| `AddressableContent/loc_zh.json` | `loc_zh` |

Optional authoring: `com.source27.cascade.modules.localizationtools` → **Cascade/更新多语言** (first run may prompt to create settings; Sheet → runtime JSON). JSON-only workflows can omit it.

Composition root: `IndieBootstrapEntry` → `GameEntry`.
