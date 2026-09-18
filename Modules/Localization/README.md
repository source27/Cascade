# Cascade Localization

Optional UPM module: Cascade's **default localization stack** (resource-backed catalog + locale tables)
plus the **Editor authoring tools** that write those JSON files.

Main package keeps only the contract `Cascade.Service.ILocalizationService`; this module owns the default
provider, the process-level widget port, the uGUI `LocalizedText` widget, and the Sheet → JSON pipeline.

## Install

Monorepo Starter (`Packages/manifest.json`):

```json
"com.source27.cascade.modules.localization": "file:../../../Modules/Localization"
```

Git URL:

```
https://github.com/source27/Cascade.git?path=Modules/Localization
```

Depends on `com.source27.cascade` (+ uGUI / TMP for `LocalizedText`). Never reverse.

> Migrating from `com.source27.cascade.modules.localizationtools` (`Modules/LocalizationTools`): same module, renamed.
> Update the manifest key/path; the settings asset, menus and JSON contract are unchanged.

## Assemblies

| Assembly | Platform | Contents |
|----------|----------|----------|
| `Cascade.Modules.Localization` | runtime | `LocalizationService`, `LocalizationDataParser`, `LocalizationAccess`, `LocalizedText`, `LocalizationInstaller` |
| `Cascade.Modules.Localization.Editor` | Editor | Sheet/CSV import pipeline, `LocalizationSyncSettings`, `Cascade → 更新多语言`, scene-view locale preview |
| `Cascade.Modules.Localization.Tests` | Editor | EditMode tests |

## Wiring

Bootstrap no longer touches localization. A Starter wires it in its own composition root / game entry:

```csharp
var localization = await LocalizationInstaller.InstallAsync(services, ct);
// registered as ILocalizationService; LocalizationAccess is bound by InitializeAsync
```

Custom stacks (Unity Localization, a project table format…) implement `Cascade.Service.ILocalizationService`
and register it themselves; this module is then unnecessary.

`LocalizationAccess` is a process-level port for AOT presentation widgets: bound on
`LocalizationService.InitializeAsync`, unbound on `Dispose`.

## Menus

| Menu | Action |
|------|--------|
| **Cascade → 更新多语言** | 若尚无 settings，询问是否创建；填好 Sheet URL 后再同步写 JSON |

创建后选中 `Assets/Settings/LocalizationSyncSettings.asset` 即可在 Inspector 改 `outputRoot` / URL。

## Output contract (runtime)

Writes under settings **`outputRoot`** (default **`Assets/Localization`** — project-owned, not a Cascade brand path):

- `localization_catalog.json` — default locale + locale code/location entries
- `localization_{locale}.json` — string tables

Runtime loads these via **resource locations** (e.g. `localization_catalog`), not the disk folder name.
Starters wire Yoo/Addressables collectors to the chosen folder.

## Settings

`LocalizationSyncSettings`:

- `defaultLocale`
- `outputRoot` — under `Assets/…`
- `sources[]` (`name`, `enabled`, Google Sheet `url`, `headerRow`, `dataStartRow`)

首次「更新多语言」创建后需先填 `url`，再执行一次同步。

## Adapters

- **Now:** Google Sheet (edit or CSV export URL)
- **Later:** Excel etc. plug the same import → JSON seam
