# Bootstrap 不含环境/版本/配置：策略归 Starter

**Status:** accepted — supersedes [ADR 0014](0014-bootstrap-cut-and-game-entry.md) 中「主包 `BootstrapConfiguration` 仅保留 `Environment`、`AppVersion`、`ResourceInitOptions` 及环境/日志辅助」一节

主包 `Cascade.Bootstrap` 只留机制与虚钩子，不再替项目定义环境分级、版本覆盖与配置容器。

## 决策

- **删除** `BootstrapConfiguration`（含 `ResolvePlayerEnvironment()` / `DefaultLogLevel()`），以及 `BootstrapBase` 上的 `[SerializeField] environment`、`appVersionOverride`、`InspectorEnvironment`、`AppVersion`、`Configuration`。
- `BootstrapEnvironment`（Dev/Beta/Gold）**也下移**到各 Starter（`Cascade.Mobile` / `Cascade.Indie` 各自的枚举）：主包对它零引用，留着就是死词汇（修订于同日，原稿曾判「留主包」）。映射成策略（日志级别、玩家侧取值）由 Starter 子类写。
- 新增虚钩子 `CreateLogService()`（默认 `UnityLogService` + `LogLevel.Info`）；`CreateResourceInitOptions()` 的返回值直接进 `IResourceService.InitializeAsync`，不再经配置容器中转。
- 组合根钩子集合：`CreateLogService` / `CreateResourceService` / `CreateResourceInitOptions` / `CreateUpdateLoop` / `RegisterServices` / `RunGameAsync`。
- Starter 侧：`MobileBootstrapEntry` 收回 `environment`、`appVersionOverride`（喂给 `MobileBootstrapConfiguration`）与日志级别映射；`IndieBootstrapEntry` 收回 `environment` 与同一映射（无版本覆盖消费方，不搬空字段）。
- `Cascade.Tests/BootstrapConfigurationTests.cs` 随类型删除——主包已无该类型可测。

**理由：** 环境分级、版本覆盖、日志级别是**项目策略**。框架不该让独立游戏与手游共用一套 dev/beta/gold 语义和版本替换规则；Starter 是 fork 起点，策略写在那里才可改。

**Considered options：** 保留 slim `BootstrapConfiguration` 作值对象（否：字段与映射都下移后，容器只剩空壳）；`BootstrapEnvironment` 留主包（否：主包零引用，只有两个 Starter 各自做同一套映射，属死词汇——已按此修订下移）；把日志策反也留在主包用钩子只给级别（否：`CreateLogService` 与 `CreateResourceService` 对称，Starter 可整体换 logger）。
