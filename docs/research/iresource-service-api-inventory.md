# IResourceService 与更新 API 现状

**Ticket:** [#19](https://github.com/source27/Cascade/issues/19)  
**Sources:** `Cascade/Runtime/Cascade.Service/Contracts/IResourceService.cs`, `Integrations/YooAsset/Runtime/YooAssetResourceService.cs`, `Cascade/Runtime/Cascade.Launcher/LauncherFlow.cs`, [ADR 0010](../adr/0010-resource-contract-load-only.md), [ADR 0003](../adr/0003-resource-layer-decoupling.md)

## 结论（给决策票）

契约 **同时包含加载与更新**。ADR 0010 要求 load-only：更新五件套 + 相关 DTO/属性应从 `IResourceService` **删除**，由 YooAsset 集成类型（或 Mobile Starter 直接打集成 API）承担。`DemoResourceService` **不存在**。

## 契约全表（`IResourceService.cs`）

### 加载语义（ADR 0010 保留候选）

| 成员 | 说明 |
|------|------|
| `bool IsInitialized` | 状态 |
| `UniTask InitializeAsync(ResourceInitOptions, CT)` | 初始化 |
| `UniTask<IAssetHandle<T>> LoadAssetAsync<T>(string, CT)` | 资源 |
| `UniTask<ISceneHandle> LoadSceneAsync(string, mode, CT)` | 场景 |
| `UniTask<byte[]> LoadRawBytesAsync(string, CT)` | 原始字节（本地化等） |
| `void UnloadUnused()` | 卸载未用 |
| `IAssetHandle<T>` / `ISceneHandle` | 句柄：`Release` / `UnloadAsync` |
| `ResourceInitOptions` | 空基类；集成子类扩展 |
| `ResourceSceneLoadMode` | Single/Additive |

### 更新语义（ADR 0010 应移出契约）

| 成员 | 说明 | `LauncherFlow` 调用 |
|------|------|---------------------|
| `string ActivePackageVersion` | 活跃包版本 | 状态/日志 |
| `bool IsUsingLocalVersion` | 离线回落本地 | CheckUpdate/Download 分支 |
| `RequestVersionAsync` | 请求远端版本 | CheckUpdate |
| `UpdateManifestAsync` | 更新清单 | CheckUpdate |
| `PrepareDownload` → `ResourceDownloadPlan` | 下载计划 | DownloadPatch |
| `DownloadAsync` + `ResourceDownloadProgress` | 下载 | DownloadPatch |
| `ClearUnusedCacheAsync` | 清未用缓存 | DownloadPatch 末 |

相关 DTO 现挂在 **同一文件 / 核心程序集**：

- `ResourceDownloadProgress`
- `ResourceDownloadPlan`（`NeedsDownload`）

`ILauncherView.SetDownloadProgress` / `WaitConfirmDownloadAsync` 依赖上述进度类型 → 随更新迁出时，**视图契约也应离开主包**（见 launcher 研究笔记）。

## YooAsset 集成（`YooAssetResourceService`）

实现 **完整** `IResourceService`（含更新）。

### 集成特有（已在集成程序集）

| 成员 | 说明 |
|------|------|
| `YooAssetResourcePlayMode` | EditorSimulate / Offline / Host |
| `YooAssetResourceInitOptions` | packageName, playMode, remoteRoot, useBuiltinPackage |
| `IDisposable` / `Dispose` | 包生命周期 |
| `ComparePackageVersion` (static public) | 版本比较（防降级） |
| `TryParsePackageVersion` (static public) | 解析 `app+seq` 形态 |

更新实现细节（`RequestPackageVersionAsync`、`LoadPackageManifestAsync`、PlayerPrefs last-known-good、builtin version 文件）均在集成内，**不在**核心契约。

## 测试假实现

`Cascade/Tests` 内多处 fake `IResourceService` 为更新方法提供 no-op（如 `AudioServiceTests`、`LocalizationTests`）。契约裁剪后 fake **变瘦**。

## 裁剪最小集合（研究建议）

**从核心删除或下沉：**

1. 接口：`RequestVersionAsync`, `UpdateManifestAsync`, `PrepareDownload`, `DownloadAsync`, `ClearUnusedCacheAsync`
2. 属性：`ActivePackageVersion`, `IsUsingLocalVersion`（若仅服务更新；若加载诊断需要版本字符串，可另议只读 `Version` 可选）
3. 类型：`ResourceDownloadProgress`, `ResourceDownloadPlan` → 迁 YooAsset 集成或 Mobile Starter
4. YooAsset 类上 **保留/公开** 同等更新 API（可不再经 `IResourceService`）
5. `LauncherFlow` 更新步骤改依赖 **具体** `YooAssetResourceService`（或 Starter 本地包装）

**核心保留：** `ResourceInitOptions` 基类 + init/load/scene/raw/unload + handles。

## 非目标

不规定最终方法命名、是否引入 `IResourceUpdateService`、或 Addressables 是否实现任何更新 API（见 Addressables 研究 + 决策票）。
