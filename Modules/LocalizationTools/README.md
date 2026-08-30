# Cascade Localization Tools

Optional **Editor** UPM module: authoring pipeline that imports tabular sources into the runtime localization JSON contract.

## Install

Monorepo Starter (`Packages/manifest.json`):

```json
"com.source27.cascade.modules.localizationtools": "file:../../../Modules/LocalizationTools"
```

Git URL:

```
https://github.com/source27/Cascade.git?path=Modules/LocalizationTools
```

Depends on `com.source27.cascade` only (tools → main; never reverse).

## Menus

| Menu | Action |
|------|--------|
| **Cascade → 更新多语言** | 若尚无 settings，询问是否创建；填好 Sheet URL 后拉取并写 JSON |
| **Cascade → 多语言设置** | 打开已有 settings；不存在时同样询问是否创建 |

Settings 不会在打开工程时静默生成。路径：`Assets/Settings/LocalizationSyncSettings.asset`。

## Output contract (runtime)

Writes under `Assets/CascadeRes/Localization/`:

- `localization_catalog.json` — default locale + locale code/location entries
- `localization_{locale}.json` — string tables

Runtime loads these via `ILocalizationService` / resource locations. This package is **not** a localization provider.

## Settings

`LocalizationSyncSettings`: `defaultLocale` + `sources[]` (`name`, `enabled`, Google Sheet `url`, `headerRow`, `dataStartRow`).

首次「更新多语言」创建后需先填 `url`，再执行一次同步。

## Adapters

- **Now:** Google Sheet (edit or CSV export URL)
- **Later:** Excel etc. plug the same import → JSON seam
