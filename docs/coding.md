# 在 Cascade 上写代码

词汇见 [`CONTEXT.md`](../CONTEXT.md)。组装见 [`assembly.md`](assembly.md)。

## 组合根

唯一注册服务、选择资源后端、决定是否热更的地方。继承 `BootstrapEntry`：

| 钩子 | 用途 |
|------|------|
| `CreateResourceService` | 返回集成包中的 `IResourceService` 实现 |
| `CreateResourceInitOptions` | 提供者专用 options |
| `RegisterServices` | `base` + 增删替换；可不注册本地化 |
| `RunGameAsync` | 主逻辑入口（必 override，否则仅警告） |

游戏专有服务：

```csharp
registry.Register<IMyFeature>(new MyFeature(...));
// 主逻辑 / 热更：
var f = host.Services.Get<IMyFeature>();
```

不要把每个服务都加成 `IGameHost` 属性。

## 服务与 ServiceRegistry

- 显式 `Register` / `Get` / `TryGet`；逆序 `Dispose`  
- **无 DI 容器**（AOT 侧约定）  
- 热更层若自建 DI，可从 `IGameHost.Services` 取根服务  

## 资源

`IResourceService`（核心）仅：

- `InitializeAsync`  
- `LoadAssetAsync` / `LoadSceneAsync` / `LoadRawBytesAsync`  
- `UnloadUnused` + 句柄 `Release`  

**没有** `RequestVersion` / `Download` 等。  

手游资源热更：持有 `YooAssetResourceService` 具体类型（或 Starter 内包装），在 `RunGameAsync` 里调其公开更新 API。DTO 名为 `YooAssetDownloadProgress` / `YooAssetDownloadPlan`。

Indie：`AddressablesResourceService`；`LoadRawBytesAsync` 的 location 须是 **TextAsset** 的 address；`UnloadUnused` 为 no-op（靠 Release 引用计数）。

## 本地化

- 契约：`ILocalizationService`（主包运行时）  
- 默认实现：catalog + 语言表，经 `LoadRawBytesAsync`  
- Google Sheet / 表导入：可选模块 `com.source27.cascade.modules.localizationtools`（Editor 作者工具，菜单 **Cascade/多语言设置**、**Cascade/更新多语言**）  
- 换配表格式：实现另一 `ILocalizationService`，在 `RegisterServices` 注册  
- 流水线：仅当 registry 中有本地化服务时才 `InitializeAsync`  

## UI

- 页面/绑定：Core + Roslyn 生成器（`[UI]` 等特性 → 注册表，运行时少反射）  
- `IGameHost.CreateUISystemAsync` / `DestroyUISystem`：UISystem 由宿主独占，不进 ServiceRegistry  
- ScaleButton、LoopScroll 列表绑定：装 `com.source27.cascade.modules.uiextras`，命名空间 `Cascade.Modules.UIExtras`  

## 事件

- `IEventBus`：`Subscribe` / `Publish` 类型化处理器  
- 本轮 **不做** Command/Query mediator 或强制 C/Q 基类；需要时在业务层自建，勿假设核心有 CQRS  

## 热更入口（仅 Mobile）

热更程序集约定（Starter / CodeLoader，**非**主包 API）：

```csharp
public static UniTask<string> Start(IGameHost host, CancellationToken cancellationToken)
```

默认类型名 `GameLogic.GameLogicEntry`（本轮 Mobile 默认；可在 Starter 配置中改）。

依赖方向：热更 → AOT（主包 + Mobile AOT），禁止反向。

## 测试习惯

- 契约与 Bootstrap 配置：EditMode，假 `IResourceService`  
- 不测私有步骤机字符串  
- Starter 冒烟：Unity Play（人工或后续 CI）  

## 不要做的事

- 在主包引用 HybridCLR 或把补丁窗塞回 Bootstrap  
- 在 `IResourceService` 上重新加更新方法  
- 用 Example/Demo 命名新的生产工程  
- 为尚未存在的第二用例先上 CQ 框架  
