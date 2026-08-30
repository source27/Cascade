# Cascade Indie Starter

Desktop/console starter: Cascade + Addressables. No HybridCLR, YooAsset, or patch window.

Unity **2022.3.62f3**. Open `Starters/Indie`.

## Run

1. Open `Assets/Scenes/Bootstrap.unity` and Play.
2. DoD: Bootstrap → Addressables + localization → `GameFlow` **Main** smoke UI → button **Enter Battle** / **Return Main**.

Composition root: `IndieBootstrapEntry` → `GameEntry` → `Cascade.Core.GameFlow`.

| Layer | Where |
| --- | --- |
| Flow runner (library) | `Cascade.Core.GameFlow` / `IGameFlowState` / `IGameFlowQuery` |
| Flow ids + states (game) | `Assets/Scripts/Flow/` (`MainFlowState`, `BattleFlowState`) |
| Context / managers hook | `IndieGameContext` (extend as the project grows) |

## Addressables

Pre-wired in `Assets/AddressableAssetsData`. JSON under `AddressableContent/` must import as **TextAsset** (`TextScriptImporter`).

| Asset | Address |
| --- | --- |
| `AddressableContent/localization_catalog.json` | `localization_catalog` |
| `AddressableContent/loc_en.json` | `loc_en` |
| `AddressableContent/loc_zh.json` | `loc_zh` |

Optional authoring: `com.source27.cascade.modules.localizationtools` → **Cascade/更新多语言**.
