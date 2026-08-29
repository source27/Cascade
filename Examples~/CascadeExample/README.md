# CascadeExample

示例工程：演示 Cascade 框架的完整用法（Unity 2022.3.62f2，内置渲染管线）。

## 快速开始

1. 用 Unity **2022.3.62f2** 打开本目录（`Examples~/CascadeExample`）。
   - 首次导入会自动从 `file:../../../`（相对 `Packages/manifest.json` 所在目录）解析并安装 `com.source27.cascade`（以及其 git 固定版本的第三方依赖，需联网）。
2. 菜单 **CascadeExample → Setup Demo Scene**（生成 Bootstrap 场景、UIRoot/Home/Detail 预制体、SFX 音效，并加入 Build Settings）。
3. 打开 `Assets/Scenes/Bootstrap.unity`，点击 **Play**。

启动链（EditorSimulate 模式）：

- `ExampleBootstrapEntry`（继承框架 `BootstrapEntry`，只覆写 `CreateResourceService()` → `DemoResourceService`）
- 启动流程：服务注册 → 本地化（内嵌词表）→ 热更程序集（编辑器内已编译的 `GameLogic.HotUpdate`）→ `GameLogicEntry.Start(IGameHost, …)`
- UI：`UIRegistryGenerated.RegisterAll`（Roslyn 源码生成器产物）→ Home 页（本地化切换 / 存档读档 / 音效 / UpdateLoop 计数 / 打开 Detail / 返回）

## 演示内容

| 按钮 | 演示 |
|---|---|
| Switch locale | `ILocalizationService.SetLocaleAsync`（en ↔ zh，内嵌词表） |
| Save / Load | `ISaveService`（PlayerPrefs） |
| Play SFX | `IAudioService.PlayOneShotAsync` |
| Update counter | `IUpdateLoop.RegisterUpdate` |
| Open Detail | `IUISystem.OpenUI` + 绑定（UIBindingHost） |

## 热更（HybridCLR）

Editor 默认 EditorSimulate（热更程序集随编辑器编译加载）。要演示**真实热更**（dll 从 StreamingAssets 加载）：

1. 菜单 `CascadeExample → HotUpdate → 1. Configure HybridCLR Settings`（把 `GameLogic.HotUpdate` 标记为热更程序集）。
2. 菜单 `CascadeExample → HotUpdate → 3. Build Hot DLL + Copy to StreamingAssets`：HybridCLR 编译热更 dll → 拷入 `Assets/StreamingAssets/GameLogic.HotUpdate.dll`，并把 Bootstrap 场景的 playMode 翻转为 **Offline**。
3. 播放：启动链走 RawFile 模式，`DemoResourceService` 从 StreamingAssets 读出 dll，`CodeLoader` 以 `Assembly.Load` 加载并反射入口 —— 改热更代码 → 重跑菜单 3 → 重进 Play 即热更闭环。

**真机构建**（Android/iOS/PC 打包）：
1. 菜单 1（配置）+ 菜单 2 `Generate AOT Metadata (All)`（生成 AOTGenericReferences）。
2. HybridCLR 安装（首次需 `ThirdParty/HybridCLR/Installer`）。
3. IL2CPP 构建 Player 后，菜单 4 `Copy AOT Metadata to StreamingAssets`（剥离后的 AOT 程序集，按 AotMetadataCatalog 的模块名分发）。
4. 打包（StreamingAssets 含热更 dll + AOT 元数据；启动时 `LoadMetadataForAOTAssembly` + `Assembly.Load`）。

Editor 内跳过 AOT 元数据加载（解释执行），热更 dll 的加载路径与真机一致。
