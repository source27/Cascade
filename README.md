# Cascade

Unity 框架包（`com.source27.cascade`，C# 命名空间 `Cascade.*`），从已停更的 Unity 工程（client，Unity 2022.3.62f2）中抽取的可复用框架层：

- **Launcher**：启动编排（组合根、启动流程、HybridCLR 热更加载、补丁窗口）
- **Service**：服务层（日志/资源/本地化/音频/存档/图集/网络契约与实现）
- **Core**：核心基础设施（UI 栈、事件总线、更新循环、生命周期、导航）
- **源码生成器**：按 `[UI]` 特性生成页面注册表与工厂，运行时零反射
- **集成包**：资源提供者可选分发（YooAsset），契约提供者无关（Addressables 留缝）

## 架构一览

```
仓库根 = UPM 包（com.source27.cascade）
├── Runtime/Cascade.Launcher/    启动编排（AOT 固化层）
├── Runtime/Cascade.Service/     服务契约 + 默认实现（AOT 固化层）
├── Runtime/Cascade.Core/        UI/事件/更新/生命周期（AOT 固化层）
├── Runtime/Cascade.Module/      模块占位（空壳）
├── Editor/Cascade.Editor/       框架支撑编辑器工具
├── Tests/Cascade.Tests/         EditMode 测试
├── Roslyn/                      源码生成器 DLL（RoslynAnalyzer 标签，全工程作用域）
├── Tools~/Cascade.SourceGenerator/  生成器源码工程（Unity 不导入）
├── Integrations/YooAsset/       可选：YooAsset 资源集成子包
└── Examples/CascadeExample/     示例 Unity 工程
```

依赖方向：`Launcher → {Service, Core, Module}`，`Core → Service`，`Module →`（无）。框架代码全部属于 **AOT 固化层**，热更代码经 `IGameHost` 访问框架设施。

## 安装

要求：Unity **2022.3** 或更新（包依赖 uGUI/TMP，需联网解析 git 固定版本的第三方包）。

### 方式一：git URL（推荐）

Unity Package Manager → **Add package from git URL**：

```
https://github.com/source27/Cascade.git
```

包在仓库根，无需 `?path=`。

### 方式二：本地路径

`Packages/manifest.json` 添加（**`file:` 路径相对于 `Packages/` 目录**，即 manifest 所在位置）：

```json
"com.source27.cascade": "file:../../cascade"
```

### 方式三：embedded

把本仓库复制到项目 `Packages/cascade/`（保留 `package.json` 与全部 `.meta`）。

### 可选：YooAsset 资源集成包

```json
"com.source27.cascade.integrations.yooasset": "https://github.com/source27/Cascade.git?path=Integrations/YooAsset"
```

安装后组合根构造 `YooAssetResourceInitOptions` 并赋给 `BootstrapConfiguration.ResourceInitOptions`（见下）。

### 依赖（随包自动解析）

| 包 | 锁定版本 | 许可 |
|---|---|---|
| com.cysharp.unitask | 2.5.11（`2e993ff`） | MIT |
| com.code-philosophy.hybridclr | 8.13.0（`ca7f87b`） | MIT |
| com.annulusgames.lit-motion (+.animation) | 2.0.2（`0b4c588`） | MIT |
| me.qiankanglai.loopscrollrect | 1.1.5（`a74a705`） | MIT |
| com.unity.ugui | 1.0.0 | Unity |
| com.unity.textmeshpro | 3.0.9 | Unity |

YooAsset（3.0.5，Apache-2.0）仅在安装集成包时引入。

## 快速开始（示例工程）

1. 用 Unity **2022.3.62f2** 打开 `Examples/CascadeExample`（首次导入自动安装本包，需联网）。
2. 菜单 **CascadeExample → Setup Demo Scene**。
3. 打开 `Assets/Scenes/Bootstrap.unity` → **Play**：启动链 → 服务注册 → 本地化 → 热更程序集加载 → Home 页演示（本地化/存档/音效/UpdateLoop/UI 导航）。

热更闭环（真实 `Assembly.Load`，Editor 内可跑）：`CascadeExample → HotUpdate` 菜单，详见 `Examples/CascadeExample/README.md`。

## 资源层

`IResourceService` 是提供者无关契约（初始化选项为基类，具体配置由组合根注入）。核心包**不依赖**任何第三方资源系统：

- **YooAsset**：安装集成包后，组合根：
  ```csharp
  config.ResourceInitOptions = new YooAssetResourceInitOptions("MyPak", YooAssetResourcePlayMode.Offline);
  ```
- **Addressables / 自研**：实现 `IResourceService`（`InitializeAsync`/`LoadAssetAsync<T>`/`LoadRawBytesAsync`/`LoadSceneAsync` + 版本/补丁方法可空实现）并同样注入即可。

## 热更（HybridCLR）

- 框架启动链含热更加载：`CodeLoader` 加载热更 dll（`Assembly.Load`）+ AOT 元数据（真机 `LoadMetadataForAOTAssembly`）。
- 入口约定：热更程序集暴露 `public static UniTask<string> Start(IGameHost host, CancellationToken)`（默认类型 `GameLogic.GameLogicEntry`，经 `BootstrapConfiguration` 可配置）。
- `EditorSimulate`（编辑器，热更程序集随编辑器编译加载）vs `Offline`/`Host`（RawFile：dll 从资源提供者读取）。

## 文档

- `CONTEXT.md` — 领域词汇（glossary）
- `docs/adr/` — 关键决策记录（布局/命名/资源解耦/DI/热更）
- `docs/agents/` — 工程技能配置（issue tracker / triage / domain docs）
- `Examples/CascadeExample/README.md` — 示例工程说明（含真机热更打包步骤）

## 状态

代码已从冻结的 client 工程抽取并改名（`DB.*` → `Cascade.*`，含设计修正）；本仓库开发机无 Unity Editor，**首次 Unity 编译验证随示例工程打开进行**（打开时自动解析 git 依赖，需联网）。
