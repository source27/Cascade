# 本地化作者工具 = 可选 Module UPM

**Status:** accepted — implements [#36](https://github.com/source27/Cascade/issues/36) / [#38](https://github.com/source27/Cascade/issues/38)

> **被 [ADR 0022](0022-ui-and-localization-out-of-core.md) 取代：** 作者管线与主包本地化实现合并为 `com.source27.cascade.modules.localization`（`Modules/Localization`）；原来的「运行时留主包」已不再成立。

Google Sheet（及后续 Excel 等）表 → 运行时 catalog/locale JSON 的 **Editor 作者管线**，以可选 Module 分发：

| 目录 | UPM name | 程序集 |
|------|----------|--------|
| `Modules/LocalizationTools/` | `com.source27.cascade.modules.localizationtools` | `Cascade.Modules.LocalizationTools.Editor`（+ Tests） |

- **运行时** `ILocalizationService` / 默认实现 / Host / `LocalizedText` / 场景 JSON 预览：留在主包。
- **依赖方向：** tools → `com.source27.cascade`；主包不引用 tools。
- **主缝：** import pipeline（启用 source → 合并 → 写出与运行时一致的 JSON）。Sheet 为第一个 adapter；Excel 另票。
- **可选性：** 仅消费已提交 JSON 的项目可不装；需要拉表时再声明 Module（见 ADR 0009）。

**Considered options：** 整包拆运行时本地化（否：Host/UI 深耦合、无第三方税）；作者工具留主包（否：第二表源会胀主包 Editor 面）。
