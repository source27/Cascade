# 研究：框架测试清单划分（client Tests → cascade 迁移清单）

> Issue #5 · 分支 `research/framework-tests` · 调研对象：`E:\Projects\client`（只读）
> 结论：42 个测试文件中共 **11 个框架测试（迁移）**、**31 个游戏测试（留下）**；URP Core / LoopScrollRect 引用只被游戏测试使用，迁移后可删除。

## 1. 测试布局总览

client 的 `Assets/Scripts/Tests` 只有两个 asmdef（来源：`Assets/Scripts/Tests/DB.Foundation.Editor.Tests.asmdef`、`Assets/Scripts/Tests/PlayMode/DB.Foundation.PlayTests.asmdef`）：

| asmdef | 平台 | rootNamespace | 引用 | 覆盖目录 |
|---|---|---|---|---|
| `DB.Foundation.Tests.Editor.Tests` | Editor | `DB.Foundation.Tests` | `DB.Foundation.Editor`、`Launcher.AOT`、`Service.AOT`、`System.AOT`、`GameLogic.HotUpdate`、`GameLogic.AOT`、`UniTask`、`LoopScrollRect.Runtime`、`UnityEngine.TestRunner`、`UnityEditor.TestRunner`、`Unity.RenderPipelines.Core.Runtime` | `Tests/` 根（9 文件）+ `Tests/GameLogic/`（32 文件） |
| `DB.Foundation.PlayTests` | 全部 | `DB.Foundation.Tests` | `GameLogic.HotUpdate`、`GameLogic.AOT`、`Service.AOT`、`System.AOT`、`Launcher.AOT`、`UniTask`、`UnityEngine.TestRunner`、`UnityEditor.TestRunner` | `Tests/PlayMode/GameLogic/`（1 文件） |

- EditMode 程序集把框架测试与游戏测试**混在同一 asmdef**；PlayMode 程序集只有 `EndlessMapScrollerTests`（游戏）→ **整个 PlayMode asmdef 不迁移**。
- 命名空间规则（client `Docs/decisions.md` ADR 0005「测试命名空间按被测层」）：`Tests/GameLogic/`、`Tests/PlayMode/GameLogic/` 用 `F13.GameLogic.Tests`，其余用 `DB.Foundation.Tests`；`Assets/Scripts/Editor/FoundationValidator.cs` 按路径/程序集名映射（`FoundationValidator.cs:167-192` 的 `ExpectedNamespaceForAssembly`/`ExpectedNamespaceForPath`）。

## 2. 迁移清单：框架测试（11 文件）

判定标准：被测类属于 `Framework/`（Launcher / Service / System，含 UI 框架类）。被测类位置均已在 client 源码中核实（文件路径见下表「被测类」列）。

### 2.1 Tests/ 根目录（9 文件，命名空间 `DB.Foundation.Tests`，无游戏依赖）

| 文件 | 被测类（命名空间） | 依赖（fixtures / asmdef） | 调整点 |
|---|---|---|---|
| `AtlasSpriteServiceTests.cs` | `AtlasSpriteService`（`DB.Service`，`Framework/Service/Resource/AtlasSpriteService.cs`）；`UIAtlasAutoPackSettings`（`DB.Foundation.Editor`，`Editor/Atlas/UIAtlasAutoPackSettings.cs`） | 私有 stub：`StubResourceService` / `StubHandle<T>` / `StubLogService`；`UniTask`；**Editor 集成**：`AssetDatabase`、`UnityEditor.U2D.SpriteAtlasUtility`、`UnityEngine.U2D.SpriteAtlas`；读真实资产 `AtlasMapping.bytes`（`UIAtlasAutoPackSettings.AtlasIndexPath`）与 `.spriteatlas` | 迁移后依赖对应 Editor 程序集（Cascade 的 Editor asmdef）与框架包内资产路径约定（`UIAtlasAutoPackSettings.DefaultAssetPath`、`DefaultIndexPath` 常量） |
| `AudioServiceTests.cs` | `AudioService`（`DB.Service`，`Framework/Service/Audio/AudioService.cs`） | `YooAssetResourceService`（`DB.Service`，经 `Service.AOT` 传入即可，测试代码不直接触碰 YooAsset 类型）；反射读私有字段 `_root` | 无 |
| `BootstrapConfigurationTests.cs` | `BootstrapConfiguration`、`AotMetadataCatalog`（`DB.Launcher`，`Framework/Launcher/`） | 无 fixture | 测试内嵌 `F13` 字符串常量（`http://10.1.51.151:2727/F13/Android/1.0.0`、`DefaultPackageName = "F13Pak"`）为框架默认值断言，迁移保留原值 |
| `FoundationValidatorTests.cs` | `FoundationValidator`（`DB.Foundation.Editor`，`Editor/FoundationValidator.cs`） | 无 fixture；**内嵌命名空间映射表**（`ExpectedRoot` helper：`Launcher.AOT→DB.Launcher`、`Service.AOT→DB.Service`、`System.AOT→DB.System`、`Module.AOT→DB.Module`、`GameLogic.HotUpdate→F13.GameLogic`、`F13.Foundation.Editor→DB.Foundation.Editor`、`F13.Foundation.Tests.Editor→DB.Foundation.Tests`） | **改名联动最大**：`FoundationValidator.cs:167-192` 与测试的 `ExpectedRoot` 都要同步 `DB.*→Cascade.*`（并处理 `F13.Foundation.Tests.Editor` 测试程序集名的归属） |
| `LauncherTextTests.cs` | `LauncherText`（`DB.Launcher`，`Framework/Launcher/LauncherText.cs`） | 无 fixture | 无 |
| `LocalizationTests.cs` | `LocalizationService`（`DB.Service`，`Framework/Service/Localization/LocalizationService.cs`）；`LocalizationSyncTool`、`LocalizationCsvSource`（`DB.Foundation.Editor`，`Editor/LocalizationSyncTool.cs`、`Editor/LocalizationSyncSettings.cs`） | 私有 fake：`FakeResourceService` / `FakeSaveService` / `FakeLogService`；`UniTask` | 依赖 Editor 程序集（`LocalizationSyncTool`） |
| `PackageVersionCompareTests.cs` | `YooAssetResourceService`（`DB.Service`，`Framework/Service/Resource/YooAssetResourceService.cs`）的 `ComparePackageVersion` / `TryParsePackageVersion` | 无 fixture | 无 |
| `ScaleButtonTests.cs` | `ScaleButton`（`DB.System`，`Framework/System/UI/ScaleButton.cs`）；`ScaleButtonEditor.ConvertToChildVisual`（`DB.Foundation.Editor`，`Editor/UI/ScaleButtonEditor.cs`） | `UnityEngine.UI`、`UnityEngine.EventSystems`（`IPointerDownHandler` 生命周期经反射调用） | 同时覆盖运行时 + 编辑器工具，迁移需 Editor asmdef |
| `UIViewTests.cs` | `UIViewAssetScope`（`DB.System`，`Framework/System/UI/UIViewContracts.cs`）；`IAssetHandle<T>`（`DB.Service`） | 私有 `TestAssetHandle` | 无 |

### 2.2 Tests/GameLogic/ 下的框架测试（2 文件，命名空间 `F13.GameLogic.Tests` —— 违反 ADR 0005）

| 文件 | 被测类 | 依赖 | 游戏依赖 | 调整点 |
|---|---|---|---|---|
| `UiCoordUtilityTests.cs` | `UiCoordUtility`（`DB.System`，`Framework/System/UI/UiCoordUtility.cs`） | 仅 `UnityEngine`（`Camera`、`Rect`） | **无** | 命名空间 `F13.GameLogic.Tests` → Cascade 测试命名空间；文件移出 `Tests/GameLogic/`（ADR 0005：命名空间随被测层；现为「游戏命名空间测框架类」违规，`FoundationValidator` 的路径映射同样会判违规） |
| `UITooltipPlacementTests.cs` | `UITooltipPlacement` / `UITooltipPlacementOptions` / `UITooltipPlacementResult` / `UITooltipArrowEdge`（`DB.System`，`Framework/System/UI/UITooltipPlacement.cs`） | 前 4 个测试纯屏幕空间计算（`Rect` 数学），无外部依赖 | **有（仅第 5 个测试）** | 见下 |

**`UITooltipPlacementTests` 必须拆分**：

- 前 4 个测试（`CenterAnchor_PrefersAbove_WithBottomArrow`、`TopAnchor_FlipsBelow_WithTopArrow`、`LeftAnchor_ClampsBubble_AndMovesArrowLeft`、`OversizedTooltip_StaysInsideViewport`）只测 `UITooltipPlacement.Place` 纯函数 → **迁移**。
- 第 5 个测试 `TooltipIsRegisteredAsTransparentTopModal` 依赖游戏侧：`F13.GameLogic.UI.UITooltip`（`GameLogic/UI/UITooltip.cs`，游戏页面）+ `F13.GameLogic.UI.Generated.UIRegistryGenerated`（**Roslyn 源码生成器产物**：`Tools/DB.UI.SourceGenerator/UISourceGenerator.cs` 扫描编译内 `[UI]` 页面后生成 `UIRegistryGenerated.g.cs`，经 `Assets/Scripts/GameLogic/Analyzers/DB.UI.SourceGenerator.dll` 注入 `GameLogic.HotUpdate` 编译，`UISourceGenerator.cs:309-316` 硬编码 `global::DB.System.UIRegistry` 等）→ **留在游戏侧**（或改写为游戏侧集成测试，与 `T05UIServiceTests` 同层）。
- 附带影响：源码生成器本身硬编码 `global::DB.System.*` 与 `F13.GameLogic.UI` 命名空间（`UISourceGenerator.cs:302-311`），`DB.*→Cascade.*` 改名时需同步更新生成器输出模板。

## 3. 留下：游戏测试（31 文件）

判定标准：被测类属于 `F13.GameLogic.*`（含游戏 UI 页面、地图、配置、编辑器地图工具）。命名空间均为 `F13.GameLogic.Tests`。

### 3.1 Tests/GameLogic/（30 文件，归属 EditMode asmdef，留下）

- **战斗引擎/规则（10）**：`BattleEngineTests`、`BattleEngineM3Tests`、`BattleStartSkillTests`、`BattleSkillCastTests`、`BattleTransitionCoordinatorTests`、`BattleUnitVitalsWidgetTests`、`BattleVitalsMathTests`（`GameLogic/UI/BattleVitalsMath.cs`）、`BattleOptionTipFormatterTests`、`C06C34SkillTests`、`HealEmptyMagVulnerabilityTests`
- **技能管线（5）**：`SkillBatchRevisionTests`、`SkillDispatcherTests`、`SkillDraftTests`、`SkillPipelineParityTests`、`SkillItemViewTests`（`F13.GameLogic.UI.Generated` 绑定）
- **局内流程/存档/事件（6）**：`RunMotorTests`、`RunSaveTests`、`RunSessionTests`、`RunBattleBridgeTests`、`EventPlayTests`、`T04GameFlowTests`
- **配置/敌人/关卡（6）**：`ConfigTablesTests`、`StageConfigValidatorTests`、`EnemyIdGroupsTests`、`EnemyDeploymentTests`、`CameraViewportControllerTests`（`F13.GameLogic.Map`）、`SegmentGeneratorTests`（`DB.Foundation.Editor` 地图编辑器工具 + `F13.GameLogic.Map`；**唯一使用 URP 类型 `UnityEngine.Rendering.Volume` 的测试**，`SegmentGeneratorTests.cs:105`）
- **UI 集成（3）**：`T05UIServiceTests`（端到端驱动 `DB.System.UISystem`/`UIRootRuntime`，但依赖游戏页面 `UITest`/`UIMain` 与 `UIRegistryGenerated`，`T05UIServiceTests.cs:186-190`）、`T05EventNavigationTests`（`F13.GameLogic.Navigation`）、`UISelectSkillListTests`（**唯一使用 `LoopScrollRect` 的测试**，`UISelectSkillListTests.cs:38`）

> 注：`RunSaveTests`、`EventPlayTests`、`StageConfigValidatorTests`、`T04GameFlowTests` 等虽 `using DB.Service`（用框架服务接口的 fake），但被测对象是 `F13.GameLogic.*`，留在游戏侧，通过框架 asmdef 公共 API 引用。

### 3.2 Tests/PlayMode/（1 文件，归属 PlayMode asmdef，整体排除）

- `EndlessMapScrollerTests`（`F13.GameLogic` 无尽地图滚动，纯游戏）→ `DB.Foundation.PlayTests` asmdef 不迁移。

## 4. 迁移后测试程序集 asmdef 引用

迁移 11 个框架测试文件后，新测试 asmdef 的引用对照（基准：`DB.Foundation.Editor.Tests.asmdef`）：

| 引用 | 处置 | 依据 |
|---|---|---|
| `Launcher.AOT`、`Service.AOT`、`System.AOT` | **保留** | 框架被测代码所在程序集 |
| `DB.Foundation.Editor` | **保留**（→ Cascade Editor asmdef） | `AtlasSpriteServiceTests`、`LocalizationTests`、`ScaleButtonTests`、`FoundationValidatorTests` 使用 |
| `UniTask` | **保留** | `AtlasSpriteServiceTests`、`LocalizationTests` 的 stub/fake 实现 `UniTask` 接口 |
| `UnityEngine.TestRunner`、`UnityEditor.TestRunner`、`nunit.framework.dll` | **保留** | EditMode NUnit |
| `GameLogic.HotUpdate`、`GameLogic.AOT` | **删除** | 11 个迁移文件无任何 `F13.GameLogic` using（见 2.1/2.2 依赖列） |
| `LoopScrollRect.Runtime` | **删除** | 全库唯一使用点 `UISelectSkillListTests.cs:38`（游戏测试） |
| `Unity.RenderPipelines.Core.Runtime`（URP Core） | **删除** | 全库唯一使用点 `SegmentGeneratorTests.cs:105`（`UnityEngine.Rendering.Volume`，游戏测试） |

## 5. 命名空间影响（`DB.*` → `Cascade.*`）

1. **9 个根测试文件**：`namespace DB.Foundation.Tests` → `Cascade` 测试命名空间（随框架仓库既定根命名空间 `Cascade`，见本仓库 `CONTEXT.md`）；`using DB.*` 机械替换为 `using Cascade.*`。
2. **`UiCoordUtilityTests` / `UITooltipPlacementTests`（拆分后）**：`F13.GameLogic.Tests` → `Cascade` 测试命名空间，且移出 `Tests/GameLogic/` 路径 —— 既修正 ADR 0005 违规，也符合 `FoundationValidator` 的「路径→命名空间」校验规则。
3. **`FoundationValidator` 联动**：`Editor/FoundationValidator.cs:167-192` 硬编码 `DB.Launcher / DB.Service / DB.System / DB.Module / DB.Foundation.Editor / DB.Foundation.Tests` 与 `F13.GameLogic` 映射、`F13.Foundation.Tests.Editor` 测试程序集名；改名后映射表、`FoundationValidatorTests.ExpectedRoot`、以及测试内 `F13.Foundation.Tests.Editor` 程序集名一并更新。
4. **源码生成器联动**：`Tools/DB.UI.SourceGenerator/UISourceGenerator.cs:302-311` 生成模板硬编码 `global::DB.System.UIRegistry`、`F13.GameLogic.UI` —— 框架侧生成器若随迁移，模板需改为 `Cascade.*`；若留在游戏侧，则生成器编译产物与框架契约版本需同步。

## 6. 来源

- 测试文件清单与命名空间：`E:\Projects\client\Assets\Scripts\Tests\`（42 个 `*.cs`，glob 全量核对）
- asmdef 引用：`Assets/Scripts/Tests/DB.Foundation.Editor.Tests.asmdef`、`Assets/Scripts/Tests/PlayMode/DB.Foundation.PlayTests.asmdef`
- 被测类位置：`Assets/Scripts/Framework/{Launcher,Service,System,Module}/`（各被测类文件见 2.1/2.2 列）
- Editor 工具类：`Assets/Scripts/Editor/{FoundationValidator.cs, LocalizationSyncTool.cs, LocalizationSyncSettings.cs, Atlas/UIAtlasAutoPackSettings.cs, UI/ScaleButtonEditor.cs}`
- 命名空间规则：`E:\Projects\client\Docs\decisions.md` ADR 0005
- 源码生成器：`E:\Projects\client\Tools\DB.UI.SourceGenerator\UISourceGenerator.cs`、`Assets/Scripts/GameLogic/Analyzers/DB.UI.SourceGenerator.dll`
- 游戏依赖证据：`Assets/Scripts/Tests/GameLogic/UITooltipPlacementTests.cs:61-67`、`T05UIServiceTests.cs:186-190`、`UISelectSkillListTests.cs:38`、`SegmentGeneratorTests.cs:105`
