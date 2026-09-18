# IResourceService load-only 最终 API

**Status:** accepted — resolves [决策：IResourceService load-only 最终 API](https://github.com/source27/Cascade/issues/23); extends [ADR 0010](0010-resource-contract-load-only.md)

## 核心契约（`Cascade.Service`）

保留：

- `bool IsInitialized`
- `UniTask InitializeAsync(ResourceInitOptions options, CancellationToken)` — **后续修订见 [ADR 0029](0029-resource-options-belong-to-provider-ctor.md)：参数只剩 `CancellationToken`，options 归 provider 构造函数，`ResourceInitOptions` 基类已删除**
- `UniTask<IAssetHandle<T>> LoadAssetAsync<T>(string location, CancellationToken)`
- `UniTask<ISceneHandle> LoadSceneAsync(string location, ResourceSceneLoadMode mode, CancellationToken)`
- `UniTask<byte[]> LoadRawBytesAsync(string location, CancellationToken)`
- `void UnloadUnused()`
- 附属：`IAssetHandle<T>`、`ISceneHandle`、`ResourceSceneLoadMode`、空基类 `ResourceInitOptions`

**删除（一次 cleave，无 Obsolete 过渡）：**

- `ActivePackageVersion`、`IsUsingLocalVersion`
- `RequestVersionAsync`、`UpdateManifestAsync`、`PrepareDownload`、`DownloadAsync`、`ClearUnusedCacheAsync`
- `ResourceDownloadProgress`、`ResourceDownloadPlan`

不设核心只读 `Version` 诊断属性。

## 更新 API 归属

资源热更表面留在 **`YooAssetResourceService` 具体类型**（集成包 public），不经 `IResourceService`。Mobile 组合根同时 `Register<IResourceService>` 并持有具体引用，在 `RunGameAsync` 内调用更新方法。

下载 DTO 迁入 `Cascade.Service.YooAsset`，改名 **`YooAssetDownloadProgress`** / **`YooAssetDownloadPlan`**。具体类上保留版本属性与 `ComparePackageVersion` / `TryParsePackageVersion`。

不引入 `IResourceUpdateService` / `IYooAssetResourceUpdater`。Addressables 集成只做 load-only，不对称实现更新。
