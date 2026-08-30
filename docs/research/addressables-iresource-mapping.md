# Addressables 映射 IResourceService 的约束

**Ticket:** [#20](https://github.com/source27/Cascade/issues/20)  
**Sources:**

- 本地：`Cascade/Runtime/Cascade.Service/Contracts/IResourceService.cs`，[ADR 0010](../adr/0010-resource-contract-load-only.md)，[ADR 0003](../adr/0003-resource-layer-decoupling.md)
- 官方：Unity Addressables 2.x  
  - [InitializeAsync manual](https://docs.unity3d.com/Packages/com.unity.addressables@2.0/manual/InitializeAsync.html)  
  - [InitializeAsync API](https://docs.unity3d.com/Packages/com.unity.addressables@2.0/api/UnityEngine.AddressableAssets.Addressables.InitializeAsync.html)  
  - [LoadAssetAsync API](https://docs.unity3d.com/Packages/com.unity.addressables@2.0/api/UnityEngine.AddressableAssets.Addressables.LoadAssetAsync.html)  
  - [AsyncOperationHandle / Release](https://docs.unity3d.com/Packages/com.unity.addressables@2.0/manual/AddressableAssetsAsyncOperationHandle.html)

## 结论（给决策票）

在 **load-only**（ADR 0010）下，Addressables **可以**实现 `IResourceService` 的 init/load/unload 核心路径。更新/编目远程刷新 **不要** 映射进核心契约；若 Starter 需要，走 Addressables 自有 API（catalog update），与 YooAsset 更新面 **刻意不对称**。

## 映射表（load-only 目标契约）

| Cascade 目标成员 | Addressables 对应 | 约束 |
|------------------|-------------------|------|
| `InitializeAsync(ResourceInitOptions)` | `Addressables.InitializeAsync` | 官方：首次 API 会自动 init；显式 init 可提前失败/加速首包。Options 子类可承载：是否 `autoReleaseHandle`、是否禁用自动 catalog 更新（settings/profile，多在 Editor 资产侧） |
| `LoadAssetAsync<T>(location)` | `Addressables.LoadAssetAsync<TObject>(key)` | key = address / label / `AssetReference` 等；**多匹配时只加载第一个**。与 YooAsset「location 字符串」同形但语义是 Addressables 键空间 |
| `LoadSceneAsync` | `Addressables.LoadSceneAsync` | 模式对应 Single/Additive；句柄释放用 Addressables 场景释放 API |
| `LoadRawBytesAsync` | 无单一一等 API | 常见做法：把 Text/`TextAsset` 当地址化资源再读字节，或 `LoadAssetAsync<TextAsset>` 包装；集成需约定 location 约定 |
| `IAssetHandle.Release` / `Dispose` | `Addressables.Release(handle)` 或 `Release(asset)` | **引用计数**；必须成对 Release，否则泄漏 |
| `UnloadUnused` | 无完美一等 API | 近似：依赖 Release 归零卸载 bundle；或项目约定调用清理。不可假想与 YooAsset `UnloadUnused` 行为相同 |
| ~~更新五件套~~ | `CheckForCatalogUpdates` / `UpdateCatalogs` / 下载 size 等 | **不进 `IResourceService`**；Indie 默认不调用 |

## 与 YooAsset 集成的不对称（预期）

| 点 | YooAsset 集成 | Addressables 集成 |
|----|---------------|-------------------|
| 初始化 options | `packageName`, playMode, remoteRoot, builtin | 更少运行时字段；大量在 AddressableAssetSettings / profiles |
| 版本字符串 | `ActivePackageVersion` 等 | 无同构「包版本」；catalog 哈希/别名另一套 |
| 资源热更编排 | Mobile Starter 打 Yoo 方言 API | Indie **不编排**；若未来要远程内容，用 Addressables catalog 流程，不复用 Yoo 流水线 |
| PlayMode 模拟 | EditorSimulate / Offline / Host | Addressables Play Mode Script（Editor） |

## Indie Starter 最低包/设置（研究建议）

1. Unity 与主包对齐（现 `package.json`：`2022.3`）
2. UPM：`com.unity.addressables`（版本 pin 策略留给决策票；建议与 2022.3 LTS 兼容的 1.21+/1.22 或 2.x 显式选定）
3. Cascade 主包 + `com.source27.cascade.integrations.addressables`（待建）
4. 工程内：至少一组 Addressable 组、一个可 load 的 address、Bootstrap 场景
5. **不**需要：HybridCLR、YooAsset、PatchWindow、构建热更窗
6. `cascade.ui.extras`：非 load 路径所必需；DoD 是否带 UI 最小页由 Indie 决策票定

## 可行性风险

1. **Raw bytes**：需约定，否则本地化若依赖 `LoadRawBytesAsync`，Addressables 集成必须实现包装策略。  
2. **Handle 模型**：Cascade 句柄 vs `AsyncOperationHandle` 适配层泄漏/双重 Release。  
3. **自动 catalog 更新**：默认 `InitializeAsync` 可能触发远程 catalog 行为——Indie「无更新」需在 settings 侧关掉或接受仅本地 catalog。  
4. **键空间**：示例/文档必须写清 address 字符串约定，避免从 Yoo location 原样照搬。

## 非目标

不实现集成包；不定 UPM 版本号最终 pin；不定 Indie 是否默认引用 ui.extras。
