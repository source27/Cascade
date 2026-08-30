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
| **Cascade → 更新多语言** | 若尚无 settings，询问是否创建；填好 Sheet URL 后再同步写 JSON |

无单独「多语言设置」菜单。创建后选中 `Assets/Settings/LocalizationSyncSettings.asset` 即可在 Inspector 改 `outputRoot` / URL。


## Output contract (runtime)

Writes under settings **`outputRoot`** (default **`Assets/Localization`** — project-owned, not a Cascade brand path):

- `localization_catalog.json` — default locale + locale code/location entries
- `localization_{locale}.json` — string tables

Runtime loads these via `ILocalizationService` **resource locations** (e.g. `localization_catalog`), not the disk folder name. Starters wire Yoo/Addressables collectors to the chosen folder.

On sync/open settings, tools publish the root to EditorPrefs so main-package scene preview can find the JSON without depending on tools types.

## Settings

`LocalizationSyncSettings`:

- `defaultLocale`
- `outputRoot` — under `Assets/…`
- `sources[]` (`name`, `enabled`, Google Sheet `url`, `headerRow`, `dataStartRow`)

首次「更新多语言」创建后需先填 `url`，再执行一次同步。

## Adapters

- **Now:** Google Sheet (edit or CSV export URL)
- **Later:** Excel etc. plug the same import → JSON seam
