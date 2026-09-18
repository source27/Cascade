# 删除未接线的 Lifecycle 骨架

**Status:** accepted — 修正 [ADR 0022](0022-ui-and-localization-out-of-core.md) 对 Core 组成的描述（其中「生命周期」一项已删除）

删除 `Cascade.Core` 的 `ILifecycleModule` 与 `LifecycleRunner`，以及 `BootstrapBase` 的 `_lifecycle` 字段、`new LifecycleRunner(log)` 与 `OnDestroy` 中的 `StopAll()`。

## 证据

- 全仓零实现、零调用：`Register` / `InitializeAsync` / `StartAll` / `ResetAll` 无任何调用点，`ILifecycleModule` 无实现类型（Modules / Integrations / 两个 Starter / 测试均无）。
- `BootstrapBase` 只构造示例后在 `OnDestroy` 对**恒空**列表调 `StopAll()`；字段私有且无属性暴露，Starter 连注册都做不到。
- 文档零提及；`git log -- Cascade/Runtime/Cascade.Core/Lifecycle` 只有 `d519de7`（搬包）一次提交，此后无接线。
- 状态机半残：`StartAll()` 靠 `_started` 提前返回，而 `StopAll()` / `ResetAll()` 不检查它。

## 理由

它声称的职责已被现有机制覆盖：服务构造与释放由 `ServiceRegistry.Dispose`（逆序）负责；启动阶段编排由 Starter 手写（Mobile `LauncherFlow` + `LauncherStage`）；业务状态由 `GameFlow` / `IGameFlowState` 负责。留一个无法使用的 public 缝只会误导，违反 ADR 0011「不为尚未存在的用例先上抽象」。

## Considered options

- **接线**（暴露 `protected LifecycleRunner Lifecycle`、流水线里 `await InitializeAsync` + `StartAll`、退出 `StopAll`，并真的写模块）：否——当前没有模块消费者，接完仍是空转。
- **原样保留**：否——不可用、无文档，还会让人以为框架支持模块生命周期。

若将来出现真需要「有序异步 init + 启停 + 重置」的第二用例，按那时的形状重新引入，而不是复活这份骨架。
