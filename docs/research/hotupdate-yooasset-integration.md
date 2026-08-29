# 研究：热更与资源管线集成细节（client → cascade 示例工程）

> 研究对象：client 仓库（Unity 2022.3 + URP 14，Android 目标为主），路径 `E:\Projects\client`。
> 产出用途：cascade 示例工程要完整演示 HybridCLR 热更；YooAsset 接入步骤供文档编写（示例工程不接 YooAsset）。
> 所有结论均以 client 源码为准，引用格式为 client 仓库内相对路径。

## TL;DR 关键结论

1. **启动链**：`BootstrapEntry`（AOT 常驻）→ 注册服务（`ServiceRegistry`）→ `LauncherFlow.RunAsync` 十阶段：Install → InitializeResource（YooAsset 包初始化）→ CheckUpdate（版本检查 + manifest 加载）→ DownloadPatch（下载补丁 + 清理缓存）→ InitializeLocalization → LoadAOTMetadata（HybridCLR 元数据注入）→ LoadGameLogicAssembly（`Assembly.Load(byte[])`）→ LaunchGame（反射调用入口）→ Completed。
2. **热更程序集加载**：`CodeLoader.LoadGameLogicAssembly(byte[])` 直接 `Assembly.Load(bytes)`；入口为反射调用 `F13.GameLogic.GameLogicEntry.Start(IGameLogicHost, CancellationToken)`，返回 `UniTask<string>`（版本号）；dll 字节来自 YooAsset `LoadRawBytesAsync("GameLogic.HotUpdate.dll")`。
3. **AOT 元数据**：`RuntimeApi.LoadMetadataForAOTAssembly(bytes, HomologousImageMode.SuperSet)`（HybridCLR 8.13.0）在启动阶段逐条注入；要注入的清单来自 `AOTGenericReferences.PatchedAOTAssemblyList`（HybridCLR Generate/All 生成），由 `AotMetadataCatalog` 反射读取并转成 YooAsset 地址。
4. **构建已工具化**：`Assets/Scripts/Editor/DBFrameworkBuildWindow.cs`（菜单 `DBFramework/构建`）一键完成 HybridCLR Generate/All → 复制热更 dll 与 AOT 元数据为 `.dll.bytes` 资产 → YooAsset ScriptableBuildPipeline 打包 → 上传 DevCDN。client 根目录**没有**构建批处理，只有 `DevCDN/RunCDN.bat`（启动开发 CDN 服务）。
5. **YooAsset**：`com.tuyoogame.yooasset@3.0.5`，`YooAssetResourceService` 包装全部流程（全局初始化、包创建、Host/Offline/EditorSimulate 三种初始化、版本请求、manifest、下载、raw bytes 加载）。

---

## 1. 启动链（BootstrapEntry → LauncherFlow）

### 1.1 入口与全局初始化（`BootstrapEntry.cs`）

`BootstrapEntry : MonoBehaviour` 挂在启动场景，序列化字段：`environment`（Dev/Beta/Gold）、`playMode`（EditorSimulate/Offline/Host）、`appVersionOverride`、`patchWindow`。

- `Awake()`：锁定 InvariantCulture（防区域设置破坏数值解析）、`DontDestroyOnLoad`、`runInBackground = true`、`targetFrameRate = 60`（`Assets/Scripts/Framework/Launcher/BootstrapEntry.cs`）。
- `Start()` 依次做四件事：
  1. 构造 `BootstrapConfiguration(environment, playMode, AppVersion)`（`BootstrapConfiguration.cs`：`AppVersion` 优先取 `appVersionOverride`，否则 `Application.version`；`AotMetadataLocations = AotMetadataCatalog.ResolveLocations()`；`AssemblyLoadMode` 由 playMode 决定，见 §2.2）。
  2. **服务注册**（`ServiceRegistry`，`BootstrapEntry.cs` `Start()`）：
     - `ILogService` → `UnityLogService`（日志级别按环境：Dev=Trace / Beta=Info / Gold=Warning，`BootstrapConfiguration.DefaultLogLevel`）
     - `IEventBus` → `EventBus(log)`
     - `IResourceService` → **`YooAssetResourceService`**（§4）
     - `IAudioService` → `AudioService(resources, log)`
     - `ISaveService` → `PlayerPrefsSaveService`
     - `ILocalizationService` → `LocalizationService(resources, save, log)`
     - `INetworkService` → `NullNetworkService`（占位，无真实网络）
     - `IAtlasSpriteService` → `AtlasSpriteService(resources, log)`
  3. 在资源系统就绪**之前**初始化补丁界面语言：`LauncherText.Initialize(save.GetString(LocalizationService.LocaleSaveKey))`——补丁 UI 词表是脚本内嵌迷你多语言字典（约 30 词条，en/zh-CN/…/ar 共 14 语言），不走任何 IO（`LauncherText.cs` 头部注释）。
  4. 装配运行时骨架：`UpdateLoop` + `UnityUpdateDriver`（MonoBehaviour 驱动帧循环）、`LifecycleRunner`、`GameLogicHost(registry, updateLoop)`，最后 `_flow = new LauncherFlow(...); _flow.Start()`。
- `OnDestroy()`：`_flow.Shutdown()`、`LifecycleRunner.StopAll()`、`LocalizationAccess.Unbind()`、`_registry.Dispose()`。

### 1.2 `LauncherFlow.RunAsync` 十阶段（`LauncherFlow.cs`）

`LauncherFlow.Start()` 以 `UniTaskVoid` 异步跑 `RunAsync(cts.Token)`，每阶段先设置 `Stage` 与 `StatusText`（走 `ILauncherView` → `PatchWindow`），失败时 `Fail(kind, ex)` 记录失败并弹错误 UI（可重试，`Retry()` 会先 `ShutdownGameLogic()`）。

| # | Stage | 行为 | 失败类型 |
|---|---|---|---|
| 1 | `Install` | 占位（仅状态），`UniTask.CompletedTask` | — |
| 2 | `InitializeResource` | 见 §1.3 | Resource |
| 3 | `CheckUpdate` | 见 §1.3 | Resource |
| 4 | `DownloadPatch` | Host 模式下载；EditorSimulate/Offline 只置状态 | Resource |
| 5 | `InitializeLocalization` | `LocalizationService.InitializeAsync` → `LocalizationAccess.Bind` | Resource |
| 6 | `LoadAOTMetadata` | 逐个位置 `LoadRawBytesAsync` → `CodeLoader.LoadMetadata`；列表为空则跳过 | Code |
| 7 | `LoadGameLogicAssembly` | `LoadRawBytesAsync("GameLogic.HotUpdate.dll")` → `CodeLoader.LoadGameLogicAssembly` | Code |
| 8 | `LaunchGame` | 先 `ShutdownGameLogic()`（调 `CodeLoader.InvokeStop`），再 `InvokeEntryAsync` 得版本号 | GameLogic |
| 9 | `Completed` | 状态文案含资源版本 `_resources.ActivePackageVersion` | — |

失败类型映射见 `BootstrapConfiguration.GetFailureKind(LauncherStage)`（Resource/Code/GameLogic 三类），用户文案见 `LauncherText` 错误常量。

### 1.3 资源相关阶段细节

- **`RunInitializeResourceAsync`**：Host 模式时 `remoteRoot = CdnRoot/{platform}/{appVersion}`（`BootstrapConfiguration.GetRemoteRoot`；Dev CDN 根 `http://10.1.51.151:2727/F13/`；平台目录 PC/Android/IPhone/WebGL 由 `GetPlatformFolder()` 决定）。构造 `ResourceInitOptions("F13Pak", playMode→ResourcePlayMode, remoteRoot, UseBuiltinPackage)`，调 `_resources.InitializeAsync`（§4.2）。
- **`RunCheckUpdateAsync`**：`RequestVersionAsync()`（远程 `F13Pak.version`，失败回退本地版本，见 §4.3）→ `UpdateManifestAsync(version)`。`IsUsingLocalVersion` 时提示"网络不可用，使用本地版本"。
- **`RunDownloadPatchAsync`**：`PrepareDownload()`（`CreateResourceDownloader` 统计）→ 有下载则经 `PatchWindow.WaitConfirmDownloadAsync` 弹确认框（或纯状态）→ `DownloadAsync(progress)` → `ClearUnusedCacheAsync()`。本地缓存不完整且连不上服务器时直接抛错（"本地资源缓存不完整…"）。

### 1.4 三种 PlayMode 与 Environment

- `BootstrapPlayMode`（`LauncherTypes.cs`）：
  - `Host`：远程包（CDN）+ 可选内置包（`UseBuiltinPackage`，仅 `DB_EMBED_PACKAGE` 定义时启用，Full 包模式打进 APK 的 `StreamingAssets/assetpack`）。
  - `EditorSimulate`：编辑器模拟资源（YooAsset EditorSimulateMode，§4.2）；热更程序集走"编辑器已加载"路径（§2.2）。仅 `UNITY_EDITOR` 可用。
  - `Offline`：纯内置包（OfflinePlayMode）。
- `BootstrapEnvironment`：Dev/Beta/Gold，由脚本宏 `DEV`/`BETA`/`GOLD` 决定（`BootstrapConfiguration.ResolvePlayerEnvironment`，`#error` 强制三选一）；Dev 才有 CDN 根，Beta/Gold 为空字符串（上线时另配）。构建时由 `SetEnvironmentDefine` 写入宏（§3.3）。

---

## 2. CodeLoader：热更程序集加载机制（`CodeLoader.cs`）

`CodeLoader` 只依赖 `ILogService` 与 `BootstrapAssemblyLoadMode`，三件事：

### 2.1 AOT 元数据注入（`LoadMetadata`）

```csharp
#if !UNITY_EDITOR
var result = RuntimeApi.LoadMetadataForAOTAssembly(bytes, HomologousImageMode.SuperSet);
if (result != LoadImageErrorCode.OK) throw ...;
#endif
```

- 仅真机（非编辑器）执行；编辑器下跳过（程序集已由编辑器加载，元数据无意义）。
- 记录字节数与 SHA256。字节来源：`LauncherFlow.RunLoadAotMetadataAsync` 用 `_resources.LoadRawBytesAsync(location)`（location 来自 `AotMetadataCatalog.ResolveLocations()`，见 §3.2）。

### 2.2 热更程序集加载（`LoadGameLogicAssembly`）

- 默认（`RawFile`）：**`Assembly.Load(bytes)`**——`byte[]` 直接走 .NET 程序集加载，无 LoadImage 自定义项。
- 编辑器 + `BootstrapAssemblyLoadMode.EditorLoaded`（由 `EditorSimulate` playMode 推导，`BootstrapConfiguration.ResolveAssemblyLoadMode`）：直接返回 `AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "GameLogic.HotUpdate")`（编辑器里热更代码已被编译进域）。

### 2.3 反射入口签名（`InvokeEntryAsync` / `InvokeStop`）

- `InvokeEntryAsync`：`assembly.GetType("F13.GameLogic.GameLogicEntry", true)` → `GetMethod("Start", Public|Static)` → 强校验参数恰好 `(IGameLogicHost, CancellationToken)`（`typeof(IGameLogicHost).IsAssignableFrom(p0)` 且 `p1 == typeof(CancellationToken)`）→ `Invoke(null, [host, cts.Token])` → 结果必须是 `UniTask<string>`，`await` 后返回（版本号）。
- `InvokeStop`：反射找 `GameLogicEntry.Stop`（public static，无参），存在则调用（幂等；不存在静默跳过）。
- 实际实现（`Assets/Scripts/GameLogic/GameLogicEntry.cs`，热更程序集内）：
  - `public static async UniTask<string> Start(IGameLogicHost host, CancellationToken cancellationToken = default)`；`public const string Version = "T05a-GameLogic-V1"`；`GameplaySceneLocation = "Gameplay"`。
  - 启动管线：加载 Gameplay 场景（`resources.LoadSceneAsync("Gameplay", Single)`）→ `host.CreateUISystemAsync(registry, "UIRootPrefab")`（注册表由生成器产出 `UIRegistryGenerated.RegisterAll`）→ `ConfigRepository`/`Tables.LoadAllAsync`（配置表，FlatBuffers）→ `GameFlow.Run()` → 返回 `Version`。
  - `Stop()`：`_flow?.Stop(); _flow = null; _ui?.ResetPageContext();`（幂等）。
- 宿主侧接口 `IGameLogicHost`（`Assets/Scripts/Framework/System/Host/IGameLogicHost.cs`，AOT 侧 `System.AOT`）：`Services / UpdateLoop / Log / Events / Resources / Localization / UI` + `CreateUISystemAsync(UIRegistry, rootAddress, ct)` / `DestroyUISystem()`。实现 `GameLogicHost`（`GameLogicHost.cs`）直接转发注册表与各服务。

### 2.4 热更字节来源

- 地址常量：`BootstrapConfiguration.DefaultGameLogicDllLocation = "GameLogic.HotUpdate.dll"`（默认 `GameLogicDllLocation`）。
- 加载路径：YooAsset 地址 → `YooAssetResourceService.LoadRawBytesAsync`（§4.4）。在 `BundleCollectorSetting.asset` 中 `Assets/F13Res/Code` 目录以 `PackRawFile` + `AddressByFileName` 收集，故资产文件名即地址（`GameLogic.HotUpdate.dll.bytes` → 地址 `GameLogic.HotUpdate.dll`，§4.5）。

---

## 3. HybridCLR 接入事实

### 3.1 包版本与配置资产

- UPM 包：`com.code-philosophy.hybridclr@8.13.0`（`Packages/manifest.json`，`file:` 引用本仓库 `Packages/` 内副本）。
- **配置资产**：`ProjectSettings/HybridCLRSettings.asset`（随仓库提交，`SettingsUtil.DefaultSettingsPath = "ProjectSettings/HybridCLRSettings.asset"`，见包内 `Editor/Settings/HybridCLRSettings.cs`）：
  - `hotUpdateAssemblyDefinitions`：`GameLogic.HotUpdate` asmdef（guid `6774e66bb59c7c74fbadba459f4de8c5`，见 `Assets/Scripts/GameLogic/GameLogic.HotUpdate.asmdef.meta`）
  - `hotUpdateDllCompileOutputRootDir: HybridCLRData/HotUpdateDlls`
  - `strippedAOTDllOutputRootDir: HybridCLRData/AssembliesPostIl2CppStrip`
  - `patchAOTAssemblies: [mscorlib]`
  - `outputLinkFile: HybridCLRGenerate/link.xml`、`outputAOTGenericReferenceFile: HybridCLRGenerate/AOTGenericReferences.cs`
- `HybridCLRData/` 是构建期生成目录（clone 中不存在，未提交）；`Assets/HybridCLRGenerate/`（link.xml、AOTGenericReferences.cs）随仓库提交。
- 程序集依赖边界：`Launcher.AOT.asmdef` 引用 `HybridCLR.Runtime`（CodeLoader 用它调 `RuntimeApi`）；`DB.Foundation.Editor.asmdef` 引用 `HybridCLR.Editor`（构建窗口用）；`FoundationValidator.cs` 固化 `Launcher.AOT` 的引用白名单（Service.AOT/System.AOT/Module.AOT/YooAsset/HybridCLR.Runtime/UniTask/UnityEngine.UI）——AOT 侧不得引用热更侧。

### 3.2 AOTGenericReferences 与 AotMetadataCatalog

- **生产**：`PrebuildCommand.GenerateAll()`（HybridCLR Generate/All）产出 `Assets/HybridCLRGenerate/AOTGenericReferences.cs`（含 `PatchedAOTAssemblyList`）与 `link.xml`。当前清单（client 实况）：`Google.FlatBuffers / LitMotion.Extensions / LitMotion / Service.AOT / System.AOT / System.Core / System / UniTask / UnityEngine.CoreModule / UnityEngine.JSONSerializeModule / mscorlib`（11 个 dll）。link.xml 额外保留 `GameLogic.AOT`（地图/表现 AOT 类）、`UnityEngine.UI`、`System` 等反射/泛型类型。
- **消费**：`AotMetadataCatalog.ResolveLocations()`（`AotMetadataCatalog.cs`）反射找 `AOTGenericReferences` 类型（先 `Type.GetType`，再全域扫描）→ 读 public static 字段 `PatchedAOTAssemblyList` → `NormalizeLocation` 只取模块文件名（去目录）→ 去重后作为 YooAsset 地址列表。空列表合法（跳过阶段）。location 约定为模块名原样（如 `mscorlib.dll`）。

### 3.3 构建步骤（已工具化，无批处理）

入口：菜单 `DBFramework/构建` → `Assets/Scripts/Editor/DBFrameworkBuildWindow.cs`（两个 tab：打包 / 打热更）。

- **打热更 tab（`BuildHotUpdatePackage`）**：`PrepareBuild(activeTarget, false)`（校验目标平台、写 `DEV/BETA/GOLD` 宏、`PlayerSettings.bundleVersion`）→ `BuildHotUpdateCode` → `BuildYooAssetPackage`。
- **`BuildHotUpdateCode`**：
  1. `PrebuildCommand.GenerateAll()`（HybridCLR Generate/All：编译热更 dll + 生成 AOTGenericReferences/link.xml）。
  2. `CopyGeneratedCode`：从 `SettingsUtil.GetHotUpdateDllsOutputDirByTarget(target)`（即 `HybridCLRData/HotUpdateDlls/{target}`）取 `GameLogic.HotUpdate.dll` → 复制为 `Assets/F13Res/Code/GameLogic.HotUpdate.dll.bytes`；再从 `SettingsUtil.GetAssembliesPostIl2CppStripDir(target)`（`HybridCLRData/AssembliesPostIl2CppStrip/{target}`）按 `ReadPatchedAotAssemblyNames()`（正则解析 AOTGenericReferences.cs 里的 dll 名）逐个复制为 `CodeRoot/{name}.bytes`。
- **`BuildYooAssetPackage`**：`UIAtlasAutoPackGenerator.GenerateAndValidate()`（图集预处理）→ `ScriptableBuildPipeline().Run(parameters)`：`EBuildPipeline.ScriptableBuildPipeline`、`EBundleType.AssetBundle`、`PackageName="F13Pak"`、`FileNameStyle=HashName`、`CompressOption=Uncompressed`、`TrackSpriteAtlasDependencies=true`（防 SpriteAtlas 白块）等 → `PackageVersionTracker.CommitBuiltVersion(packageVersion)`（写 `ProjectSettings/F13PackageVersion.json`，格式 `应用版本_序号`，当前 `0.0.1_2`）。
- **打包 APK tab（`BuildPackageAndApk`）**：Full 模式（嵌入 `StreamingAssets/assetpack` + `DB_EMBED_PACKAGE` 宏）/ Lite 模式（不嵌入）→ 同上热更代码 + YooAsset 包 → `BuildPipeline.BuildPlayer` 出 APK。
- **上传 DevCDN（`UploadResourcesToDevCdn`）**：HTTP PUT 每个文件到 `http://10.1.51.151:2727/F13/{platform}/{appVersion}/`，带 `X-CSRF-Token: 227e24ff-…`；`.version` 文件最后上传。服务端：`DevCDN/devcdn-server.mjs`（`node devcdn-server.mjs` 启动，`RunCDN.bat`），支持 GET/PUT + CSRF 校验，默认监听 `10.1.51.151:2727`。
- 结论：**构建完全工具化**（Editor 窗口一键，内部调 HybridCLR.Editor API）；client 根目录无构建批处理脚本。

---

## 4. YooAsset 接入事实

### 4.1 包与全局初始化

- UPM 包：`com.tuyoogame.yooasset@3.0.5`（openupm scoped registry，`Packages/manifest.json`）。
- `YooAssetResourceService.InitializeAsync`（`Assets/Scripts/Framework/Service/Resource/YooAssetResourceService.cs`）：
  - `if (!YooAssets.IsInitialized) YooAssets.Initialize();`（全局单例，无参数）
  - `YooAssets.TryGetPackage(packageName)` 已有则复用，否则 `YooAssets.CreatePackage("F13Pak")`。
  - 包已 `InitializeStatus == Succeeded` 则直接返回（幂等）。

### 4.2 Package 初始化（三模式 → `InitializePackageAsync`）

- **EditorSimulate**（仅编辑器）：`EditorSimulateBuildInvoker.Build(packageName, (int)EBundleType.VirtualAssetBundle)`（YooAsset 内置，`Packages/com.tuyoogame.yooasset@3.0.5/Runtime/PackageBuilder/EditorSimulateBuildInvoker.cs`）→ `EditorSimulateModeOptions` + `FileSystemParameters.CreateDefaultEditorFileSystemParameters(simulateBuild.PackageRootDirectory)`，追加 `VirtualWebglMode / VirtualDownloadMode / VirtualDownloadSpeed / AsyncSimulateMinFrame(1)/MaxFrame(3)` 参数。
- **Offline**：`OfflinePlayModeOptions { BuiltinFileSystemParameters = FileSystemParameters.CreateDefaultBuiltinFileSystemParameters() }`（读 `StreamingAssets` 内置包）。
- **Host**：`HostPlayModeOptions`：
  - `BuiltinFileSystemParameters`：仅 `UseBuiltinPackage`（`DB_EMBED_PACKAGE` 宏）时启用，并加 `EFileSystemParameter.CopyBuiltinPackageManifest = true`（把内置 manifest 拷入沙盒）；
  - `CacheFileSystemParameters = FileSystemParameters.CreateDefaultSandboxFileSystemParameters(remoteService)`，其中 `remoteService` 是自定义 `IRemoteService`（内部类 `RemoteService`：`GetRemoteUrls(fileName) => [$"{remoteRoot}/{fileName}"]`）——即 CDN 根就是 `http://10.1.51.151:2727/F13/{platform}/{appVersion}`；
  - 沙盒下载参数：`DownloadMaxConcurrency=5`、`DownloadMaxRequestPerFrame=1`、`DownloadWatchdogTimeout=10`。
- 失败语义：`operation.Status != Succeeded` → 抛 `operation.Error`。

### 4.3 版本检查 / manifest / 下载

- **`RequestVersionAsync`**：先 `ResolveLocalPackageVersionAsync`（本地已知好版本：PlayerPrefs `f13.yooasset.{appVersion}.{package}.lastKnownGoodVersion` ⊕ 内置包版本文件 `StreamingAssets/assetpack/{package}/{package}.version`，取高者）→ `RequestPackageVersionOptions(true, 8)`（远程 `F13Pak.version`，`YooAssetConfiguration.GetPackageVersionFileName`）→ 成功则做**防降级**比较（`ComparePackageVersion`：`应用版本_序号`，序号按 int 比），失败且有本地版本 → `_isUsingLocalVersion = true` 用本地，否则抛错。
- **`UpdateManifestAsync`**：`LoadPackageManifestAsync(new LoadPackageManifestOptions(version, 15))`；失败且非本地模式时回退本地版本 manifest（同样置 `_isUsingLocalVersion`）。
- **下载**：`PrepareDownload()` → `CreateResourceDownloader(new ResourceDownloaderOptions(5, 3))`（并发 5、失败重试 3）统计 `TotalDownloadCount/TotalDownloadBytes` → `DownloadAsync` 轮询 `IsDone` 报进度 → `ClearUnusedCacheAsync`（`ClearUnusedBundleFiles` + `ClearUnusedManifestFiles`）。
- **`ActivePackageVersion`**：`_package.GetPackageVersion()`。
- 内置目录约定：`YooFolderName` 取自 `Assets/Resources/YooAssetSettings.asset`（`YooFolderName: assetpack`），内置包根为 `StreamingAssets/assetpack`。

### 4.4 原始字节加载（热更 dll / AOT 元数据 / 配置 / 本地化都走它）

`LoadRawBytesAsync(location)`：
1. `_package.GetAssetInfo(location)` 解析地址（失败抛错）。
2. **编辑器模拟模式**：直接 `File.ReadAllBytes(项目根/assetInfo.AssetPath)`（虚拟包无真实 bundle）。
3. 真机：`_package.LoadAssetAsync(assetInfo)` → `GetRawAssetBytes(handle.AssetObject)`：`RawFileObject.GetBytes()`（.bytes/raw 资产）或 `TextAsset.bytes` → 释放 handle。

### 4.5 Collector 规则（`Assets/BundleCollectorSetting.asset`）

包 `F13Pak`（EnableAddressable=1），关键组：
- `Code`：收集 `Assets/F13Res/Code`，`CollectorType=0`（MainAssetCollector）、`PackRawFile`、`AddressByFileName`、`CollectAll` → 热更 dll 与 AOT 元数据以**原文件名**为地址（`GameLogic.HotUpdate.dll`、`mscorlib.dll`…），且每个文件独立原始文件包（不打包成 AssetBundle，直接裸文件）。
- `Localization`：`Assets/F13Res/Localization`，`PackRawFile`、`AddressByFileName`（地址 `localization_catalog` 等，`LocalizationService.DefaultCatalogLocation`）。
- `Config`：`Assets/F13Res/Config`，`PackRawFile`、`AddressByFolderAndFileName`。
- 其余：Prefab/Scene（PackSeparately）、SpriteAtlas、SpriteSource（静态、不可寻址）、RuntimeCatalog（图集映射 bytes）、PostProcess。

---

## 5. 示例工程必须复刻的内容（完整热更 demo）

> 一个能自证"热更生效"的最小工程至少需要：

1. **HybridCLR 依赖与配置**
   - `com.code-philosophy.hybridclr@8.13.0` 加入 manifest（git 固定版本，见 #2 研究）。
   - `ProjectSettings/HybridCLRSettings.asset`：`hotUpdateAssemblyDefinitions` 指向热更 asmdef；输出目录 `HybridCLRData/HotUpdateDlls`、`HybridCLRData/AssembliesPostIl2CppStrip`；`patchAOTAssemblies` 至少含 `mscorlib`；`outputLinkFile/outputAOTGenericReferenceFile` 指到 `HybridCLRGenerate/`。
   - 首次需执行 HybridCLR 安装（`HybridCLR/Installer`，需下载本地 il2cpp 补丁，client 由 `HybridCLRData/LocalIl2CppData-…` 承载，未入库）。
2. **AOT / 热更程序集分层**（client 模式）：
   - AOT 契约层：`Service.AOT`（服务接口 + `IGameLogicHost` 依赖的服务面）、`System.AOT`（UI 框架接口/UI 系统）、`Module.AOT`、`GameLogic.AOT`（表现层 AOT 类）——全部**不引用**热更层；
   - 热更层：`GameLogic.HotUpdate.asmdef`（rootNamespace `F13.GameLogic`，引用 AOT 层 + UniTask + 表现依赖）；热更 asmdef 必须显式列在 HybridCLRSettings。
   - 启动器 AOT 侧引用 `HybridCLR.Runtime`（`Launcher.AOT.asmdef`）。
3. **启动器**（可精简为 client 的 BootstrapEntry + LauncherFlow + CodeLoader + GameLogicHost + PatchWindow + LauncherText + AotMetadataCatalog + BootstrapConfiguration，或等价简化版），必须实现：
   - 服务注册（至少 ILogService / ISaveService / IResourceService / ILocalizationService / IEventBus）；
   - 资源初始化 → 版本检查 → 补丁下载（确认 UI）→ 本地化 → **AOT 元数据注入** → **dll 加载** → **反射入口** 的完整顺序（§1.2）；
   - `CodeLoader.LoadMetadata`（`RuntimeApi.LoadMetadataForAOTAssembly(bytes, HomologousImageMode.SuperSet)`，仅真机）；
   - `CodeLoader.LoadGameLogicAssembly`（`Assembly.Load(byte[])`；编辑器 EditorSimulate 时复用已编译程序集）；
   - `InvokeEntryAsync`：反射 `F13.GameLogic.GameLogicEntry.Start(IGameLogicHost, CancellationToken)` → `UniTask<string>`；`InvokeStop` 反射 `Stop()`（幂等）。
4. **热更侧入口**：`F13.GameLogic.GameLogicEntry`（static class）：`Start` 内完成 场景加载 → UI 系统 → 配置表 → 主流程，返回版本串；`Stop` 可重复调用。
5. **AOT 元数据与 dll 资产**：热更 dll 与 `PatchedAOTAssemblyList` 里每个 dll 都作为 `.bytes` 资产放进资源包（地址=模块名，如 `GameLogic.HotUpdate.dll`、`mscorlib.dll`）；`AotMetadataCatalog`（或等价物）反射读 `AOTGenericReferences.PatchedAOTAssemblyList` 得到地址清单。
6. **构建管线**（工具化）：
   - HybridCLR Generate/All（`PrebuildCommand.GenerateAll()`）→ 复制 `HybridCLRData/HotUpdateDlls/{target}/GameLogic.HotUpdate.dll` → `Code/GameLogic.HotUpdate.dll.bytes`；复制 `HybridCLRData/AssembliesPostIl2CppStrip/{target}/` 下的补丁 dll → `Code/{name}.bytes`；
   - YooAsset 打包（ScriptableBuildPipeline，Code 组 PackRawFile/AddressByFileName）→ 产物上传 CDN 目录 `{CDNRoot}/{platform}/{appVersion}/`（`.version` 文件最后传）；
   - 版本号 `应用版本_序号`（如 `0.0.1_2`）贯穿 CDN 目录与版本文件；
   - `link.xml` 保留反射/泛型类型（HybridCLRGenerate/link.xml 随仓库提交）。
7. **验证手段**：改热更代码（如 GameLogicEntry.Version 或 UI 文案）→ 重新 Generate/All + 打包 → 上传 → 旧客户端启动发现新版本并下载 → 运行后可见改动生效；以此证明"热更链路"闭环。

---

## 6. YooAsset 文档需覆盖的接入步骤（示例工程不接 YooAsset）

文档（README/示例说明）需给出如下接入步骤，示例工程本身**不**引入 YooAsset：

1. **依赖**：`com.tuyoogame.yooasset@3.0.5`（openupm scope `com.tuyoogame.yooasset`）；运行时 asmdef 引用 `YooAsset`。
2. **全局初始化**：启动时 `if (!YooAssets.IsInitialized) YooAssets.Initialize();` → `CreatePackage(packageName)`（幂等，TryGetPackage 复用）。
3. **Package 初始化（三模式）**：EditorSimulate（`EditorSimulateBuildInvoker.Build` + EditorSimulateModeOptions）/ Offline（BuiltinFileSystemParameters）/ Host（BuiltinFileSystemParameters 可选 + `CreateDefaultSandboxFileSystemParameters(自定义 IRemoteService)`；`CopyBuiltinPackageManifest=true` 让内置首包进入沙盒缓存体系；下载并发/看门狗参数）。
4. **版本检查**：`RequestPackageVersionAsync(RequestPackageVersionOptions(timeout))` 拿 `{Package}.version` → `LoadPackageManifestAsync(LoadPackageManifestOptions(version, timeout))`；本地回退与防降级逻辑可参考 `YooAssetResourceService.RequestVersionAsync/UpdateManifestAsync`。
5. **补丁下载**：`CreateResourceDownloader(options)` → `StartDownload()` → 轮询 `IsDone` 报进度 → 成功后 `ClearCacheAsync(ClearUnusedBundleFiles/ManifestFiles)`；"最近已知好版本"用 PlayerPrefs 持久化（key `f13.yooasset.{appVersion}.{package}.lastKnownGoodVersion`）。
6. **资源加载**：`LoadAssetAsync<T>(location)` / `LoadSceneAsync` / `LoadRawBytesAsync(location)`（raw 文件/TextAsset 取字节，供 dll/元数据/配置/本地化使用）。
7. **Collector 配置**：`BundleCollectorSetting.asset`——热更代码组用 `PackRawFile` + `AddressByFileName`（裸文件、文件名即地址），常规资产用 PackSeparately/PackDirectory；`YooFolderName` 可在 `Resources/YooAssetSettings.asset` 配（client 用 `assetpack`，内置包根 `StreamingAssets/assetpack`）。
8. **构建集成**：`ScriptableBuildPipeline().Run(ScriptableBuildParameters)`（HashName 文件名、Uncompressed、`TrackSpriteAtlasDependencies` 等）；产物目录结构 `{BuildOutputRoot}/{target}/{package}/{version}/`，上传 CDN 时 `{package}.version` 最后放。

---

## 7. 引用文件清单（client）

| 主题 | 文件 |
|---|---|
| 启动入口/服务注册 | `Assets/Scripts/Framework/Launcher/BootstrapEntry.cs` |
| 启动流程十阶段 | `Assets/Scripts/Framework/Launcher/LauncherFlow.cs` |
| 热更加载/元数据注入/反射入口 | `Assets/Scripts/Framework/Launcher/CodeLoader.cs` |
| 配置（CDN/包名/dll 地址/宏） | `Assets/Scripts/Framework/Launcher/BootstrapConfiguration.cs` |
| AOT 元数据清单解析 | `Assets/Scripts/Framework/Launcher/AotMetadataCatalog.cs` |
| 宿主实现 / 宿主接口 | `Assets/Scripts/Framework/Launcher/GameLogicHost.cs`、`Assets/Scripts/Framework/System/Host/IGameLogicHost.cs` |
| 补丁 UI / 迷你词表 | `Assets/Scripts/Framework/Launcher/PatchWindow.cs`、`LauncherText.cs`、`ILauncherView.cs`、`LauncherTypes.cs` |
| 启动器 asmdef | `Assets/Scripts/Framework/Launcher/Launcher.AOT.asmdef` |
| YooAsset 包装 | `Assets/Scripts/Framework/Service/Resource/YooAssetResourceService.cs` |
| 资源服务契约 | `Assets/Scripts/Framework/Service/Contracts/IResourceService.cs` |
| 本地化服务 | `Assets/Scripts/Framework/Service/Localization/LocalizationService.cs` |
| 构建工具（一键热更/APK/上传） | `Assets/Scripts/Editor/DBFrameworkBuildWindow.cs` |
| 版本号追踪 | `Assets/Scripts/Editor/PackageVersionTracker.cs` + `ProjectSettings/F13PackageVersion.json` |
| 程序集引用白名单 | `Assets/Scripts/Editor/FoundationValidator.cs` |
| HybridCLR 配置资产 | `ProjectSettings/HybridCLRSettings.asset` |
| HybridCLR 生成物 | `Assets/HybridCLRGenerate/AOTGenericReferences.cs`、`Assets/HybridCLRGenerate/link.xml` |
| 热更程序集/入口 | `Assets/Scripts/GameLogic/GameLogic.HotUpdate.asmdef`、`Assets/Scripts/GameLogic/GameLogicEntry.cs`、`Assets/Scripts/GameLogic/AOT/GameLogic.AOT.asmdef` |
| 热更资产（dll/元数据 .bytes） | `Assets/F13Res/Code/*.dll.bytes` |
| YooAsset Collector 配置 | `Assets/BundleCollectorSetting.asset` |
| YooAsset 目录名配置 | `Assets/Resources/YooAssetSettings.asset` |
| 包版本（UPM） | `Packages/manifest.json` |
| HybridCLR 包内 API | `Packages/com.code-philosophy.hybridclr@8.13.0/Editor/Settings/{SettingsUtil,HybridCLRSettings}.cs` |
| YooAsset 包内 API | `Packages/com.tuyoogame.yooasset@3.0.5/Runtime/PackageBuilder/EditorSimulateBuildInvoker.cs`、`Runtime/Settings/YooAssetConfiguration.cs` |
| DevCDN 服务 | `DevCDN/devcdn-server.mjs`、`DevCDN/RunCDN.bat` |
