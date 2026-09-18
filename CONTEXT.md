# Cascade

Unity 游戏客户端能力库与可 fork Starter 的领域词汇。本文件是 glossary，不是规范或设计文档。

## Language

**Cascade**:
本仓库的核心 UPM 能力库（`com.source27.cascade` 及由其拆出的可选子包）。提供可组合能力与默认可替换的 Bootstrap 骨架，不绑定玩法，**不负责**代码热更或资源热更编排。
_Avoid_: 框架全家桶、引擎、中台、热更框架

**能力（Capability）**:
Cascade 对外提供的一块可独立理解、可按需依赖的功能边界（如事件、资源契约、Bootstrap、UI 底座、本地化栈）。可选能力以独立 UPM 子包分发，不进主包默认依赖。
_Avoid_: 插件、插件包、Module（作统称时）

**集成包（Integration）**:
对接外部系统的可选 UPM 子包，实现 Cascade 契约（如 `IResourceService` 的 YooAsset / Addressables 实现）。依赖方向：集成包 → Cascade，永不反向。资源后端的「版本检查 / 下载」等方言 API 留在集成包（或 Starter 对集成类型的调用），不提升进核心契约。
_Avoid_: 第三方封装、adapter 包（口语可说 adapter，正式称集成包）

**UI 扩展包（UI Extras）**:
可选 UPM 子包（约定名 `cascade.ui.extras`），承载依赖第三方 UI 库的能力（如 LoopScroll 列表绑定、LitMotion 驱动的 ScaleButton）。UI 栈（`cascade.modules.ui`）不依赖 LitMotion / LoopScrollRect。
_Avoid_: 塞进主包的 UI 工具、仅存在于某个 Starter 的列表/按钮动画（若跨项目复用）

**UI 栈（UI Stack）**:
可选 UPM 模块 `com.source27.cascade.modules.ui`（`Modules/UI`，程序集/命名空间 `Cascade.Modules.UI`）：`UISystem` 页面栈、`UIBase`/`UIRegistry`/`UIContextId` 等 UI 底座、`IAtlasSpriteService` 图集精灵服务、Roslyn 页面注册表生成器与其编辑器工具。**UI 实例归游戏**（游戏入口创建、自存、自释放），不挂在宿主上。不装则主包无 uGUI/TMP 依赖。
_Avoid_: 把 UI 底座当主包必装能力、在主包或 Core 引用 uGUI/TMP、让宿主持有 UI

**启动编排（Bootstrap）**:
主包程序集 **`Cascade.Bootstrap`**（原 `Cascade.Launcher`）内的薄默认流水线：注册服务（`CreateLogService`/`CreateResourceService` 等钩子）→ 建 Host → 解析/注册 `IUpdateLoop` → 资源 **初始化** → 虚钩子 `RunGameAsync` 交主逻辑。Starter 在钩子内接热更/直入游戏（本地化安装、UI 创建都在这里或更后）。**环境分级、版本覆盖、日志级别、资源 options 等策略写在 Starter 子类**（含各自的 `BootstrapEnvironment` 枚举，见 `MobileBootstrapEntry`/`IndieBootstrapEntry`），主包不含这些字段与类型，也没有 `BootstrapConfiguration`。**不含** HybridCLR、资源版本下载、补丁窗、构建窗。
_Avoid_: 热更包、cascade.hotupdate、框架管热更、默认跑资源更新、Cascade.Launcher（旧名）、Cascade.Module（已删空壳）、纯零件无流水线（已否决）

**本地化提供者**:
实现 `ILocalizationService` 的运行时。**契约**在主包 `Cascade.Service`；**默认实现**（经资源加载 catalog 与语言表）在可选模块 `com.source27.cascade.modules.localization`，由 `LocalizationInstaller.InstallAsync` 安装（初始化即绑定 `LocalizationAccess`，Dispose 解绑）。可换成 Unity Localization、本地配表等其它实现而只依赖契约。
_Avoid_: 把 Sheet 当成唯一本地化实现、无契约的硬编码文案服务、把作者工具包当成运行时提供者

**本地化作者工具（Localization Tools）**:
与默认实现同属模块 `com.source27.cascade.modules.localization` 的 Editor 半边：Google Sheet 等表源 → 运行时同款 catalog/locale JSON（schema 单一来源，不再跨包靠约定对齐）。不装该模块则主包仍可在场景里预览已提交的 JSON 之外无本地化栈。
_Avoid_: 塞进主包的 Sheet 同步、runtime 依赖 Excel/Sheet 库、与 `ILocalizationService` 混为一谈

**Starter**:
可 fork 的完整 Unity 工程，开新项目的生产起点。路径约定：`Starters/Mobile`、`Starters/Indie`。薄壳（最小场景/入口证明组装通），不承载可玩内容切片。Mobile 拥有代码热更 + 资源热更全套（HybridCLR、YooAsset 更新编排、Patch UI、构建窗）；Indie 使用 Addressables，无热更。
_Avoid_: Example、示例工程、Demo、Sample、Examples/

**组合根（Composition Root）**:
工程内唯一负责注册服务并启动流程的入口。库提供默认可 override 的基类/骨架；最终注册集、资源后端、是否热更属于 Starter（或具体项目）。

**宿主（Host）**:
主逻辑入口通过 `IGameHost` 拿到的**唯一门面**：`IGameHost.Services`（服务注册表）。日志、事件、资源、更新循环、本地化、UI 一律从注册表取，由游戏自建 context 保存；Host 不再缓存服务、不再持有 UI 实例。游戏专有服务进注册表，不扩 `IGameHost`。
_Avoid_: 把 `IGameHost` 当服务目录、在 Host 上挂 UI/本地化、为方便再加属性

**服务注册表（ServiceRegistry）**:
显式 `Register` / `Get` 的服务容器；不使用 DI 框架。扩展方式：组合根 override 注册，而非改宿主类型。

**更新循环（Update Loop）**:
注册表服务 `IUpdateLoop`：消费者 `RegisterUpdate` / `RegisterLateUpdate` / `RegisterFixedUpdate` 拿 `IDisposable`；驱动者（`UnityUpdateDriver` 或自实现）调 `Tick*`。由 Bootstrap 在 `RegisterServices` 之后解析（已注册者优先，否则 `CreateUpdateLoop`）或组合根自注册；实现 `IDisposable`，随注册表释放。
_Avoid_: 把 `UpdateLoop` 具体类型塞进宿主、让每个消费者各自 `AddComponent` 一个 MonoBehaviour 循环

**资源提供者（Resource Provider）**:
`IResourceService` 的具体实现。**默认实现** `UnityResourcesService`（`UnityEngine.Resources` + `SceneManager`）在主包，作为 Bootstrap 的默认资源服务；YooAsset / Addressables 等实现在集成包。核心契约限于 init/load/unload 等加载语义；**不含**更新语义。换 provider 只改组合根的 `CreateResourceService()`。

**代码热更 / 资源热更**:
仅 Mobile Starter（及 fork 项目）领域内的概念与实现。Cascade 主包不依赖 HybridCLR，也不编排资源更新。

**AOT 固化层**:
随主程序编译发布的稳定代码。在采用代码热更的项目中，它是热更层依赖的基座；在无热更项目中，主逻辑也可同属 AOT。

**热更层**:
经 HybridCLR 运行时加载、可独立更新的代码。只存在于需要代码热更的 Starter/项目。

**默认流水线（Default Pipeline）**:
Bootstrap 提供的可替换启动骨架（默认服务、资源 **初始化**、交付 `IGameHost` 给主逻辑入口约定）。不包含资源更新或代码热更步骤；那些由 Mobile Starter 在组合根之后（或之中 override）自行接上。

**游戏流程（GameFlow）**:
主包 `Cascade.Core` 提供的薄流程骨架：`IGameFlowState`（Enter/Exit 自管该步 UI）+ `GameFlow`（注册状态、`RunAsync` / `ChangeStateAsync` / `ReturnAsync`：先 Exit 再 Enter；单槽 `PreviousStateId`，非多级栈）+ `IGameFlowQuery`（只读当前/上一 id）。**状态 id 与表由游戏程序集定义**（string，如 Main/Battle）；核心不设封闭枚举、不提供通用 FSM/Procedure 栈。与 `UIContextId`（UI 树分区）正交。
_Avoid_: 把 Login/Battle 做成 UIContextId、在核心包写死业务流程表、GF 式 FsmState 全家桶、把 GameFlow 做成导航回退栈
