# 热更架构：AOT 固化层 + HybridCLR 热更层

框架代码全部属于 AOT 固化层（编译进主程序集，元数据最小化）；热更代码经 HybridCLR 加载并反射入口（`public static UniTask<string> Start(IGameHost, CancellationToken)`，入口类型/热更 dll 地址/程序集名经 `BootstrapConfiguration` 可配置，默认 `GameLogic.GameLogicEntry` / `GameLogic.HotUpdate.dll` / `GameLogic.HotUpdate`）。`CodeLoader` 双模式：`EditorSimulate`（编辑器内复用已编译程序集）与 RawFile（`Assembly.Load` dll 字节，真机另经 `LoadMetadataForAOTAssembly` 加载 AOT 元数据）。

## 理由

client 的两层编译架构（AOT 固化层 + 热更层）是经过生产验证的设计，抽取时原样保留并参数化 client 侧硬编码（F13 入口串、CDN、YooAsset 包名）；示例工程 Editor 内即可跑通真实 `Assembly.Load` 热更路径（Offline/RawFile），真机 AOT 元数据步骤文档化。依赖方向：热更 → AOT，禁止反向。
