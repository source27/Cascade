# Cascade 领域词汇

本文件是领域词汇表（glossary），不是规范或设计文档；术语随任务落地持续更新。实现细节见 `docs/` 与各 ADR。

- **cascade**：本仓库交付的 Unity 框架包。UPM 包名 `com.source27.cascade`，C# 根命名空间 `Cascade`。从已停更的 client 工程抽取，client 仅作复制来源、冻结不动。
- **框架层**：可复用的基础代码，按程序集分层：Launcher（启动编排）、Service（服务契约与实现）、Core（UI/更新/事件/生命周期等核心基础设施）、Module（模块占位）。程序集名与命名空间同名（如 Cascade.Launcher），四层均属 AOT 固化层。SpriteAnimation 子系统属 client 游戏侧实现，不属框架层。
- **AOT 固化层**：编译进主程序集、随包发布的代码，是热更层依赖的稳定基座。
- **热更层**：经 HybridCLR 运行时加载、可独立更新的代码（示例工程演示完整热更流程）。框架提供加载与入口反射机制。
- **组合根（composition root）**：启动入口，负责注册服务并驱动启动流程。
- **宿主（Host）**：热更入口通过 `IGameHost` 访问框架设施（服务注册表/更新循环/日志/事件/UI 系统）；UISystem 由宿主独占创建与销毁（`CreateUISystemAsync` / `DestroyUISystem`），不进服务注册表。
- **服务注册表（ServiceRegistry）**：显式注册/获取服务的容器，不使用 DI 框架。
- **资源提供者（Resource Provider）**：`IResourceService` 的具体实现，以可选集成子包分发（如 `Integrations/YooAsset/`）；核心包只含提供者无关的契约，启动流程经契约初始化资源。Addressables 等未来提供者同构加入。
- **UI 框架**：页面/视图/绑定基础设施与 Roslyn 源码生成器（生成页面注册与绑定代码，运行时零反射）。
- **示例工程（Example Project）**：`Examples/` 下独立 Unity 工程，演示框架能力与热更流程。
