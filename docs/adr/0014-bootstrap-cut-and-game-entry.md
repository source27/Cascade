# Bootstrap 切割与主逻辑入口约定

**Status:** accepted — resolves [决策：Bootstrap 切割与主逻辑入口约定](https://github.com/source27/Cascade/issues/22)

> **部分被 [ADR 0022](0022-ui-and-localization-out-of-core.md) 取代：** 本地化不再由 Bootstrap 注册/初始化；`IGameHost` 只剩 `Services`。

主包 `Cascade.Bootstrap` 提供 **薄默认流水线**（非纯零件、非热更全家桶）：

`RegisterServices` → `UpdateLoop` + `GameHost` → `IResourceService.InitializeAsync` →（若已注册）`ILocalizationService.InitializeAsync` → `RunGameAsync(IGameHost, CT)` 虚钩子。

- 默认 `RunGameAsync`：Warning + no-op。Starter override：Indie 同进程进主逻辑；Mobile 在钩子内做资源更新（集成方言 API）+ HybridCLR/`CodeLoader` + 热更入口。
- 热更入口签名为 **Mobile 约定**（非主包 API）：`public static UniTask<string> Start(IGameHost, CancellationToken)`。
- 迁出主包：`CodeLoader`、`AotMetadataCatalog`、胖启动流的更新/补丁步骤、`ILauncherView`/`PatchWindow`/`LauncherText`、`PlayMode`/`AssemblyLoadMode` 与热更 Config 字段。
- 主包 `BootstrapConfiguration` 仅保留 `Environment`、`AppVersion`、`ResourceInitOptions` 及环境/日志辅助。
- 本地化：`ILocalizationService` 与「catalog+表经资源加载」的默认实现留主包；Google Sheet 同步为作者工具；其它配表格式用另一实现替换注册。流水线仅在已注册时 init。
