# 代码/资源热更归属 Mobile Starter（取代 ADR 0005）

**Status:** accepted — **supersedes ADR 0005**

Cascade **不负责**代码热更与资源热更编排：主包不依赖 HybridCLR；不提供 CodeLoader、AOT 元数据目录、Patch 流程/UI、与 HybridCLR+YooAsset 绑定的构建窗口。这些实现与依赖留在 **Mobile Starter**（及 fork 出的项目）源码中。不设 `cascade.hotupdate` UPM——热更复用路径是 fork Starter，不是再引一个热更包。Indie Starter 无热更，主逻辑 AOT 直入。

**理由：** 热更与具体后端/发行流水线强绑定；放进核心会强迫独立游戏承担 HybridCLR 税，也与「能力库不管组装热更」矛盾。私用下第二个手游 = 再 fork Mobile，UPM 抽热更包的收益不足以多一个产品面。

**Bootstrap 仍可：** 默认服务注册、`IResourceService` 初始化、交付 `IGameHost`；**不**默认跑版本检查/下载或加载热更 DLL。
