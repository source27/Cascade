# CascadeExample

示例工程：演示 Cascade 框架的完整用法（Unity 2022.3.62f2，内置渲染管线）。

## 快速开始

1. 用 Unity **2022.3.62f2** 打开本目录（`Examples/CascadeExample`）。
   - 首次导入会自动从 `file:../../` 解析并安装 `com.source27.cascade`（以及其 git 固定版本的第三方依赖，需联网）。
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

## 热更

Editor 内为 EditorSimulate（热更程序集随编辑器编译加载）。真机/构建热更（HybridCLR 构建 + StreamingAssets 分发）见「示例工程接入 HybridCLR 热更演示」任务（`CascadeExample → Build HotUpdate`）。
