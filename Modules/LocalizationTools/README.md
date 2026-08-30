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
| **Cascade → 多语言设置** | Create/select `Assets/Settings/LocalizationSyncSettings.asset` |
| **Cascade → 更新多语言** | Download enabled Google Sheet sources and write JSON |

## Output contract (runtime)

Writes under `Assets/CascadeRes/Localization/`:

- `localization_catalog.json` — default locale + locale code/location entries
- `localization_{locale}.json` — string tables

Runtime loads these via `ILocalizationService` / resource locations. This package is **not** a localization provider.

## Settings

`LocalizationSyncSettings`: `defaultLocale` + `sources[]` (`name`, `enabled`, Google Sheet `url`, `headerRow`, `dataStartRow`).

Empty URL on sync opens settings and fails with guidance.

## Adapters

- **Now:** Google Sheet (edit or CSV export URL)
- **Later:** Excel etc. plug the same import → JSON seam
