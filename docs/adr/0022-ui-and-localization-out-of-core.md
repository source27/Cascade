# UI 底座与本地化出主包，Host 收窄为服务注册表

**Status:** accepted — supersedes [ADR 0013](0013-upm-package-and-assembly-inventory.md)（UI 程序集归属）、[ADR 0014](0014-bootstrap-cut-and-game-entry.md)（流水线本地化步骤）、[ADR 0020](0020-localization-authoring-tools-module.md)（作者工具模块名）

主包从「UI + 本地化栈常驻」收敛为「契约 + 事件/更新/流程 + 注册表」。四条决策一起做，避免两次全量重命名。

> **后续修正：** 其中「生命周期」一项已删除，见 [ADR 0024](0024-remove-unwired-lifecycle.md)。

## 1. UI 底座 = 可选 Module `Modules/UI`

| 项 | 值 |
|----|----|
| 目录 | `Modules/UI/` |
| UPM name | `com.source27.cascade.modules.ui` |
| 程序集 | `Cascade.Modules.UI`（runtime）、`Cascade.Modules.UI.Editor`、`Cascade.Modules.UI.Tests` |
| 命名空间 | `Cascade.Modules.UI` / `Cascade.Modules.UI.Editor` / `Cascade.Modules.UI.Tests` |

- 迁入：`UISystem`、`UIBase`、`UIRoot`、`UIRegistry`、`UIContracts`、`UIViewContracts`、`WorldUIHost`、`SafeArea`、`UITooltipPlacement`、`UICoordUtility`、`UIBindingHost`、`UIPointerClickRelay`、`EmptyRaycast`；Editor 的 `UIScriptGenerator` / `UIGenerationLayout` / `UIBindingHostEditor` / `ComponentAddListener`；UI EditMode 测试；Roslyn 生成器（`Modules/UI/Roslyn/Cascade.SourceGenerator.dll`，源码 `Modules/UI/Tools~/Cascade.SourceGenerator/`）。
- 主包相应去掉 `com.unity.ugui` / `com.unity.textmeshpro` 依赖，以及 `Cascade.Core` / `Cascade.Bootstrap` / `Cascade.Editor` 的 `UnityEngine.UI`、`Unity.TextMeshPro` 引用。
- 生成器常量与生成代码改 `Cascade.Modules.UI.*`；未装模块时生成器 `GetTypeByMetadataName` 返回 null、自然空转。
- `Modules/UiExtras` 的 runtime 去掉对 `Cascade.Core` 的死引用（其运行时代码本就不使用 Cascade 类型）。

**理由：** 主包必须能被「不用 Cascade UI 栈」的工程干净引用；uGUI/TMP 依赖与 ~100KB UI 底座是能力选项，不是底座税（ADR 0008/0009 方向）。

## 2. 本地化 = 可选 Module `Modules/Localization`

| 项 | 值 |
|----|----|
| 目录 | `Modules/Localization/` |
| UPM name | `com.source27.cascade.modules.localization`（原 `...modules.localizationtools`） |
| 程序集 | `Cascade.Modules.Localization`、`Cascade.Modules.Localization.Editor`、`Cascade.Modules.Localization.Tests` |
| 命名空间 | `Cascade.Modules.Localization*` |

- **契约** `Cascade.Service.ILocalizationService` 留在 `Cascade.Service/Contracts`（与资源层同构：契约在核心、实现在可选包）。
- 迁入模块：`LocalizationService`、`LocalizationDataParser`、`LocalizationAccess`、`LocalizedText`（uGUI/TMP 组件，随实现走）、Editor 场景预览（原 `Cascade.Editor/Localization`）、Sheet→JSON 作者管线（原 `Modules/LocalizationTools`）。
- 新增 `LocalizationInstaller.InstallAsync(IServiceRegistry, ct)`：构造 → 初始化 → 注册；`LocalizationService.InitializeAsync` 成功后自绑定 `LocalizationAccess`，`Dispose` 解绑。
- Bootstrap：删 `RegisterDefaultLocalization` 钩子、删流水线本地化步骤、删 `LocalizationAccess` 绑定；默认流水线回到「注册服务 → 资源 init → `RunGameAsync`」。

**Considered options：** 契约一并搬进模块（否：主包词汇表少一个服务契约，且项目无法只依赖契约换实现）；作者工具与运行时分成两包（否：同一 JSON schema 分居两包、靠 EditorPrefs 传路径）。

## 3. UISystem 归游戏，不进 Host

- `IGameHost.UI` / `CreateUISystemAsync` / `DestroyUISystem` 删除。
- 创建：`UISystem.CreateAsync(IServiceRegistry services, UIRegistry registry, string rootAddress, CancellationToken ct)`，依赖（`IResourceService` / `IUpdateLoop` / `ILogService`）自行从注册表解析。
- 所有权：调用方持有（游戏自己的 context / 入口静态字段）。注册表无 unregister，重试路径需先 `Dispose` 再创建；Mobile 由热更入口 `Stop()` 释放。

## 4. Host 只剩服务注册表

`IGameHost { IServiceRegistry Services { get; } }`（类型保留：热更入口签名 `Start(IGameHost, CancellationToken)` 与 AOT link.xml 稳定）；`GameHost` 只持注册表。游戏侧自建 context，从注册表取所需服务并自行保存（Indie `IndieGameContext`、Mobile `PageContext` 即此形状）。

## 5. UpdateLoop 注册为服务

- `IUpdateLoop` 增加 `TickUpdate/TickLateUpdate/TickFixedUpdate`（驱动者视角）——**后续修订见 [ADR 0032](0032-update-loop-contract-and-implementations.md)：Tick* 已收回实现，契约只留 Register*，默认实现自持宿主**，`UpdateLoop : IUpdateLoop, IDisposable`，`UnityUpdateDriver.Bind(IUpdateLoop)`。
- `BootstrapBase` 在 `RegisterServices` 之后解析：注册表里已有 `IUpdateLoop` 则采用，否则 `CreateUpdateLoop(log)` 创建并注册；`RegisterServices` 期间 `IUpdateLoop` 不可用。

## 迁移

| 旧 | 新 |
|----|----|
| `Cascade.Core.*`（UI 类型） | `Cascade.Modules.UI.*` |
| `Cascade.Service.{LocalizationService,LocalizationDataParser,LocalizationAccess}` | `Cascade.Modules.Localization.*` |
| `Cascade.Editor.{EditorLocalizationPreview,LocalizationEditorPaths,LocalizationSceneToolbar}`、`Cascade.Editor.{UIScriptGenerator,UIGenerationLayout,UIBindingHostEditor,ComponentAddListener}` | `Cascade.Modules.Localization.Editor.*` / `Cascade.Modules.UI.Editor.*` |
| `com.source27.cascade.modules.localizationtools`（`Modules/LocalizationTools`） | `com.source27.cascade.modules.localization`（`Modules/Localization`） |
| `host.Resources|Log|Events|Localization|UI|UpdateLoop` | `host.Services.Get<T>()`；UI 由游戏创建 |
| `host.CreateUISystemAsync(...)` | `UISystem.CreateAsync(services, registry, "UIRoot", ct)` |
| Bootstrap 自动注册/初始化本地化 | `await LocalizationInstaller.InstallAsync(services, ct)` |

`FoundationValidator` 白名单同步：`Cascade.Core` 只允许 `Cascade.Service` / `UniTask`；`Cascade.Bootstrap` 去掉 `UnityEngine.UI`。
