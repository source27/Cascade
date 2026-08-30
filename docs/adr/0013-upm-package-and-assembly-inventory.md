# 改造后 UPM 包与程序集清单

**Status:** accepted — resolves [决策：改造后 UPM 包与程序集清单](https://github.com/source27/Cascade/issues/21)

| 目录 | UPM name | 程序集 |
|------|----------|--------|
| `Cascade/` | `com.source27.cascade` | `Cascade.Service`, `Cascade.Core`, **`Cascade.Bootstrap`**（原 `Cascade.Launcher`）, `Cascade.Editor`, `Cascade.Tests` |
| `Integrations/YooAsset/` | `com.source27.cascade.integrations.yooasset` | `Cascade.Service.YooAsset` |
| `Integrations/Addressables/` | `com.source27.cascade.integrations.addressables` | `Cascade.Service.Addressables` |
| `Modules/UiExtras/` | `com.source27.cascade.modules.uiextras` | `Cascade.Modules.UIExtras` |
| 未来能力 | `Modules/<Name>/` | `com.source27.cascade.modules.<name>` → `Cascade.Modules.<Name>` |

- **删除** 空壳 `Cascade.Module`（不再作可选能力归宿，见 ADR 0009）。
- 依赖方向：Modules / Integrations → 主包；主包不反向依赖。
- 主包不另拆 `Cascade.UI` 程序集，也不把 Editor/Tests 拆成独立 UPM。
- 更新 ADR 0002：四层含 Module / Launcher 的表述以本 ADR 为准。
