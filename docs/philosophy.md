# Cascade 设计思想

术语见根目录 [`CONTEXT.md`](../CONTEXT.md)。硬决策见 [`docs/adr/`](adr/)。

## 双交付物

Cascade 同时交付两样东西：

1. **能力库**（UPM：`com.source27.cascade` 及可选子包）— 可组合、可替换的客户端底座  
2. **Starter**（`Starters/Mobile`、`Starters/Indie`）— 可 fork 的生产起点，不是演示 Demo  

库通过文档说明如何组装；Starter 是组装结果的薄壳模板。受众以作者自用为主（公司手游 + 个人独立游戏）。

## 能力可组合，不拥有热更

主包提供：

- 薄 **Bootstrap** 流水线与组合根基类  
- **Service** 契约与默认实现（日志、音频、存档…；本地化只留契约）  
- **Core** 基础设施（事件、更新循环、生命周期、GameFlow）  

可选 Module 提供：

- **UI 栈**（`com.source27.cascade.modules.ui`）：页面/视图基类、UI 树分区、Roslyn UI 生成器  
- **本地化栈**（`com.source27.cascade.modules.localization`）：默认 provider + Sheet→JSON 作者工具  
- **UI 扩展**（`com.source27.cascade.modules.uiextras`）：ScaleButton / LoopScroll

主包 **不** 负责：

- HybridCLR / 代码热更加载  
- 资源版本检查与下载编排  
- 补丁 UI、与具体后端绑定的构建窗  

代码热更与资源热更属于 **Mobile Starter**（及 fork 出的项目）。独立游戏走 Addressables，不背 HybridCLR 税。

## 依赖方向

```
Modules/*  ──►  Cascade（主包）
Integrations/* ──►  Cascade（主包）
Starter  ──►  Cascade + 选定的 Integrations / Modules
```

永不反向：主包不依赖任何集成包或 Starter。

## 薄默认流水线

Bootstrap 默认顺序：

1. `RegisterServices`（日志从 `CreateLogService()` 来）  
2. 解析/注册 `IUpdateLoop` + 建 `IGameHost`（只有 `Services`）  
3. `IResourceService.InitializeAsync`（options 来自 `CreateResourceInitOptions()`）  
4. 虚方法 `RunGameAsync(IGameHost, CancellationToken)`  

默认 `RunGameAsync`：警告 + no-op。差异全在 Starter override：

- **Mobile**：本地化安装（`LocalizationInstaller`）→ Yoo 更新 API → HybridCLR / CodeLoader → 热更 `Start`（热更入口自建并持有 UISystem）  
- **Indie**：本地化安装 → 同进程 `GameEntry.Start`（烟测 UI 自带）

## 资源契约 load-only

`IResourceService` 只有 init / load / unload。版本与下载 API 留在具体集成类型上（如 `YooAssetResourceService`），由 Mobile 组合根持有具体引用并调用。Addressables 集成刻意不对称：只做加载，不封装 catalog 更新。

## 扩展方式

- 组合根 override：`CreateLogService`、`CreateResourceService`、`CreateResourceInitOptions`、`CreateUpdateLoop`、`RegisterServices`、`RunGameAsync`（环境分级、版本覆盖、日志级别等策略写在 Starter 子类，见 ADR 0023）  
- 游戏专有服务：`Register` 进 `ServiceRegistry`，热更/主逻辑经 `host.Services.Get<T>()`  
- 游戏流程：主包 `GameFlow` + 游戏实现 `IGameFlowState`（状态 id 自定）；**不**提供 GF 式通用 FSM  
- **不**使用 DI 容器（AOT 侧）；**不**把 `IGameHost` 扩成服务目录（只剩 `Services`）  
- 本地化：契约在主包；默认实现 + 作者工具在 `modules.localization`，由 Starter 安装；可换成别家实现  
- UI：`modules.ui` 提供底座，UISystem 由游戏创建并持有，宿主不参与

## 可选能力

真正可选 = 独立 UPM（`Modules/UI`、`Modules/Localization`、`Modules/UiExtras`、未来红点/多设备输入等），不是「主包永远编译进来但不 Register」。
