# Cascade

Unity 游戏客户端能力库与可 fork Starter 的领域词汇。本文件是 glossary，不是规范或设计文档。

## Language

**Cascade**:
本仓库的核心 UPM 能力库（`com.source27.cascade` 及由其拆出的可选子包）。提供可组合能力与默认可替换的 Bootstrap 骨架，不绑定玩法，**不负责**代码热更或资源热更编排。
_Avoid_: 框架全家桶、引擎、中台、热更框架

**能力（Capability）**:
Cascade 对外提供的一块可独立理解、可按需依赖的功能边界（如 UI 底座、事件、资源契约、Bootstrap）。可选能力以独立 UPM 子包分发，不进主包默认依赖。
_Avoid_: 插件、插件包、Module（作统称时）

**集成包（Integration）**:
对接外部系统的可选 UPM 子包，实现 Cascade 契约（如 `IResourceService` 的 YooAsset / Addressables 实现）。依赖方向：集成包 → Cascade，永不反向。资源后端的「版本检查 / 下载」等方言 API 留在集成包（或 Starter 对集成类型的调用），不提升进核心契约。
_Avoid_: 第三方封装、adapter 包（口语可说 adapter，正式称集成包）

**UI 扩展包（UI Extras）**:
可选 UPM 子包（约定名 `cascade.ui.extras`），承载依赖第三方 UI 库的能力（如 LoopScroll 列表绑定、LitMotion 驱动的 ScaleButton）。主包 UI 底座不依赖 LitMotion / LoopScrollRect。
_Avoid_: 塞进主包的 UI 工具、仅存在于某个 Starter 的列表/按钮动画（若跨项目复用）

**启动编排（Bootstrap）**:
主包程序集 **`Cascade.Bootstrap`**（原 `Cascade.Launcher`）内的薄默认流水线：注册服务 → 建 Host → 资源 **初始化** →（若已注册）本地化初始化 → 虚钩子 `RunGameAsync` 交主逻辑。Starter 在钩子内接热更/直入游戏。**不含** HybridCLR、资源版本下载、补丁窗、构建窗。
_Avoid_: 热更包、cascade.hotupdate、框架管热更、默认跑资源更新、Cascade.Launcher（旧名）、Cascade.Module（已删空壳）、纯零件无流水线（已否决）

**本地化提供者**:
实现 `ILocalizationService` 的运行时；默认实现经资源加载 catalog 与语言表。可替换为本地配表等其它实现并在组合根注册。
_Avoid_: 把 Sheet 当成唯一本地化实现、无契约的硬编码文案服务、把作者工具包当成运行时提供者

**本地化作者工具（Localization Tools）**:
可选 UPM 模块 `com.source27.cascade.modules.localizationtools`（Editor-only）：Google Sheet 等表源 → 运行时同款 catalog/locale JSON。依赖方向：tools → 主包。不装则仍可用已提交的 JSON + 主包场景预览。
_Avoid_: 塞进主包的 Sheet 同步、runtime 依赖 Excel/Sheet 库、与 `ILocalizationService` 混为一谈

**Starter**:
可 fork 的完整 Unity 工程，开新项目的生产起点。路径约定：`Starters/Mobile`、`Starters/Indie`。薄壳（最小场景/入口证明组装通），不承载可玩内容切片。Mobile 拥有代码热更 + 资源热更全套（HybridCLR、YooAsset 更新编排、Patch UI、构建窗）；Indie 使用 Addressables，无热更。
_Avoid_: Example、示例工程、Demo、Sample、Examples/

**组合根（Composition Root）**:
工程内唯一负责注册服务并启动流程的入口。库提供默认可 override 的基类/骨架；最终注册集、资源后端、是否热更属于 Starter（或具体项目）。

**宿主（Host）**:
主逻辑入口通过 `IGameHost` 访问的稳定门面：服务注册表、更新循环、日志、事件、资源、本地化、UI 系统所有权等。游戏专有服务进注册表，不随意扩 `IGameHost` 属性。

**服务注册表（ServiceRegistry）**:
显式 `Register` / `Get` 的服务容器；不使用 DI 框架。扩展方式：组合根 override 注册，而非改宿主类型。

**资源提供者（Resource Provider）**:
`IResourceService` 的具体实现，只存在于集成包。核心契约限于 init/load/unload 等加载语义；**不含**更新语义。

**代码热更 / 资源热更**:
仅 Mobile Starter（及 fork 项目）领域内的概念与实现。Cascade 主包不依赖 HybridCLR，也不编排资源更新。

**AOT 固化层**:
随主程序编译发布的稳定代码。在采用代码热更的项目中，它是热更层依赖的基座；在无热更项目中，主逻辑也可同属 AOT。

**热更层**:
经 HybridCLR 运行时加载、可独立更新的代码。只存在于需要代码热更的 Starter/项目。

**默认流水线（Default Pipeline）**:
Bootstrap 提供的可替换启动骨架（默认服务、资源 **初始化**、交付 `IGameHost` 给主逻辑入口约定）。不包含资源更新或代码热更步骤；那些由 Mobile Starter 在组合根之后（或之中 override）自行接上。
