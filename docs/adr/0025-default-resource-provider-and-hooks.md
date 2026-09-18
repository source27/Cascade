# 主包默认资源实现 = UnityEngine.Resources；默认服务逐个可换

**Status:** accepted（在 [ADR 0015](0015-iresource-service-load-only-api.md) 的 load-only 契约内补上默认提供者，不改变该契约）

主包新增 `UnityResourcesService`（`Cascade.Service`，只用 `UnityEngine.Resources` + `SceneManager`），并成为 `BootstrapBase.CreateResourceService()` 的默认值。

## 决策

- **默认组合**：日志 = `UnityLogService`；资源 = `UnityResourcesService`（`Resources/` 文件夹、原始字节用 TextAsset、场景进 Build Settings）；存档 = `PlayerPrefsSaveService`；音频 = `AudioService`。全部在主包，只用 Unity 自带 API。（网络服务一项已改：见 [ADR 0027](0027-remove-network-service.md)——`INetworkService` 已删除。）（图集精灵一项已改：见 [ADR 0026](0026-atlas-sprite-service-belongs-to-ui.md)——服务归 `Modules/UI`、不再默认注册。）
- **每个默认都由独立钩子产出**：`CreateLogService` / `CreateResourceService` / `CreateResourceInitOptions` / `CreateSaveService` / `CreateAudioService` / `CreateUpdateLoop`。返回 null = 不注册该服务（`log` 与 `resource` 是流水线必需，返回 null 直接报错）。
- **追加**自定义服务：`override RegisterServices` 里 `base` 之后 `registry.Register<T>(…)`。
- 集成包（YooAsset / Addressables）从「必需」变为「需要 bundle / catalog / 更新编排时才装」。

**理由：** 核心包只保留最基础的基础与最基础的实现——不装任何集成包的工程应当能跑通；同时每个默认都要能**单独**替换，而不是靠「不调 base」来换（那样会连带丢掉其它默认）。异步签名保持，换 provider 不动调用点。

**Considered options：** 资源默认继续为 null + 抛错（否：不装集成包跑不起来，违背「提供默认实现」）；把 Resources provider 写到各 Starter（否：重复两份，且主包将没有可用默认）；只加 `CreateResourceService` 而不加其余钩子（否：存档/音频/网络/图集仍只能靠不调 base 替换）。
