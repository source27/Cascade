# 如何组装 Cascade

## 包一览

| 路径 | UPM name | 何时安装 |
|------|----------|----------|
| `Cascade/` | `com.source27.cascade` | 总是 |
| `Integrations/YooAsset/` | `com.source27.cascade.integrations.yooasset` | 需要 Yoo 资源管线 / 资源热更编排时（默认用主包 `UnityResourcesService`） |
| `Integrations/Addressables/` | `com.source27.cascade.integrations.addressables` | 需要 Addressables catalog/远端加载时 |
| `Integrations/Desktop/` | `com.source27.cascade.integrations.desktop` | PC 壳：local/roaming 设置、JSON 多槽存档、音频/光标/日志等（需 `com.unity.inputsystem`） |
| `Integrations/Steam/` | `com.source27.cascade.integrations.steam` | Steam-only（依赖 Desktop；peer Steamworks.NET） |
| `Integrations/InputGlyphs/` | `com.source27.cascade.integrations.inputglyphs` | 手柄/键鼠 Glyph 桥接（peer InputGlyphs `com.eviltwo.input-glyphs`；两者必须同装） |
| `Modules/UI/` | `com.source27.cascade.modules.ui` | 需要 Cascade UI 栈（页面/视图/生成器、图集精灵服务）时 |
| `Modules/UiExtras/` | `com.source27.cascade.modules.uiextras` | 需要 ScaleButton / LoopScroll 时（依赖 `modules.ui`） |
| `Modules/Localization/` | `com.source27.cascade.modules.localization` | 需要默认本地化栈（资源 catalog）或 Google Sheet→JSON 等作者工具时 |

主包程序集：`Cascade.Service`、`Cascade.Core`、`Cascade.Bootstrap`、`Cascade.Editor`、`Cascade.Tests`。

依赖方向：Integrations / Modules → 主包；Starter → 任选。Steam → Desktop → 主包。UI 栈与本地化栈互不依赖。

## 安装主包

Unity 2022.3+。Package Manager → Add from git URL：

```
https://github.com/source27/Cascade.git?path=Cascade
```

本地 monorepo（相对 `Packages/manifest.json`）：

```json
"com.source27.cascade": "file:../../../Cascade"
```

（路径按工程位置调整。）

### 可选包

```json
"com.source27.cascade.integrations.yooasset": "https://github.com/source27/Cascade.git?path=Integrations/YooAsset",
"com.source27.cascade.integrations.addressables": "https://github.com/source27/Cascade.git?path=Integrations/Addressables",
"com.source27.cascade.integrations.desktop": "https://github.com/source27/Cascade.git?path=Integrations/Desktop",
"com.source27.cascade.integrations.steam": "https://github.com/source27/Cascade.git?path=Integrations/Steam",
"com.source27.cascade.integrations.inputglyphs": "https://github.com/source27/Cascade.git?path=Integrations/InputGlyphs",
"com.source27.cascade.modules.ui": "https://github.com/source27/Cascade.git?path=Modules/UI",
"com.source27.cascade.modules.uiextras": "https://github.com/source27/Cascade.git?path=Modules/UiExtras",
"com.source27.cascade.modules.localization": "https://github.com/source27/Cascade.git?path=Modules/Localization"
```

Addressables 集成 pin `com.unity.addressables` **1.21.19**（与 Indie Starter 一致）。  
Desktop 依赖 `com.unity.inputsystem` **1.14.2**（写在 Desktop `package.json`；工程需可解析）。

### 第三方 peer 依赖（工程 manifest 提供）

`package.json` **只**列 Unity 注册表可解析的依赖（`com.unity.*`）和本仓库包（`com.source27.cascade*`）。  
第三方（UniTask / LitMotion / YooAsset 等）是 **peer**：写在工程 `manifest.json`（git/file 或 OpenUPM），不进各 UPM `package.json`。  
原因：SemVer 硬依赖会在「仅 Add package from git URL」时被 UPM 当注册表包解析，直接 `cannot be found`（见 ADR 0021）。

| Peer 包 | 版本 | 需要它的包 |
|--------|------|-----------|
| com.cysharp.unitask | 2.5.11 | 主包 / Modules(UI, Localization) / YooAsset / Addressables |
| com.annulusgames.lit-motion | 2.0.2 | UiExtras |
| com.annulusgames.lit-motion.animation | 2.0.2 | UiExtras |
| me.qiankanglai.loopscrollrect | 1.1.5 | UiExtras |
| com.tuyoogame.yooasset | 3.0.5 | YooAsset 集成 |

```json
"com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2e993ff18f28c931602a07292df0b0804eebef99",
"com.annulusgames.lit-motion": "https://github.com/AnnulusGames/LitMotion.git?path=src/LitMotion/Assets/LitMotion#0b4c588ee75a07198841d92aab653e6b39445089",
"com.annulusgames.lit-motion.animation": "https://github.com/AnnulusGames/LitMotion.git?path=src/LitMotion/Assets/LitMotion.Animation#0b4c588ee75a07198841d92aab653e6b39445089",
"me.qiankanglai.loopscrollrect": "https://github.com/qiankanglai/LoopScrollRect.git#a74a705e1c9d0f73ea1a441dabb23b22f3283071",
"com.tuyoogame.yooasset": "https://github.com/tuyoogame/YooAsset.git?path=Assets/YooAsset#94422fc41491228eed0999ce4845d7b23ee2b8ae"
```

主包 git URL 可单独添加；缺 peer 时是**编译**失败，不再是 Package Manager 解析失败。Starter 已写入上述 pin。

主包 **不再** 依赖 uGUI/TMP、HybridCLR、LitMotion、LoopScrollRect、YooAsset。

## 最快路径：fork Starter

### 手游 — `Starters/Mobile`

1. Unity **2022.3.62f3** 打开 `Starters/Mobile`  
2. `Assets/Scenes/Bootstrap.unity` → Play  
3. 组合根：`MobileBootstrapEntry`（Yoo + 热更流水线）  
4. 构建：菜单 **Cascade/构建窗口**（HybridCLR + Yoo 打包/热更）
5. 细节：`Starters/Mobile/README.md`  

### 独立游戏 — `Starters/Indie`

1. 打开 `Starters/Indie`  
2. `Assets/Scenes/Bootstrap.unity` → Play  
3. 组合根：`IndieBootstrapEntry` → `GameEntry`（全 AOT，无热更）  
4. 细节：`Starters/Indie/README.md`  
5. 可选 PC / Steam 壳接线：`Starters/Indie/Assets/Scripts/DesktopShell/README.md`

## 从零组装（不 fork Starter）

1. 空工程安装主包（默认已经能跑：Unity 日志 / Unity `Resources` / PlayerPrefs；需要 bundle、catalog 或资源更新编排时再加 `Integrations/*`）  
2. 场景挂载继承 `Cascade.Bootstrap.BootstrapBase` 的组合根
3. Override：

```csharp
// 环境/版本/日志级别等策略由你自己的子类持有（Inspector 字段 + ResolveMinimumLogLevel）
protected override ILogService CreateLogService() =>
    new UnityLogService { Enabled = true, MinimumLevel = LogLevel.Info };

protected override IResourceService CreateResourceService() =>
    new YooAssetResourceService(); // 或 AddressablesResourceService

protected override ResourceInitOptions CreateResourceInitOptions() =>
    new YooAssetResourceInitOptions(...); // 或 AddressablesResourceInitOptions

protected override async UniTask RunGameAsync(IGameHost host, CancellationToken ct)
{
    var services = host.Services;
    // 需要本地化：await LocalizationInstaller.InstallAsync(services, ct);
    // 需要 UI：var ui = await UISystem.CreateAsync(services, uiRegistry, "UIRoot", ct);（自行保存/释放）
    // Mobile: 资源更新（Yoo 具体 API）+ CodeLoader + 热更 Start
    // Indie: await GameEntry.Start(host, ct);
}
```

4. 需要 UI 栈时装 `modules.ui`；需要 UI 动效/循环列表时再装 `modules.uiextras`
5. 需要默认本地化栈（或 Sheet 作者工具）时装 `modules.localization`
6. 需要 PC / Steam 壳时再装 Desktop（+ 可选 Steam / InputGlyphs），见下文

## 默认流水线止点

Bootstrap **只到** 资源 init 和 `RunGameAsync`（`IUpdateLoop` 在 `RegisterServices` 之后注册）。  
本地化安装、UI 创建、资源 **更新**、HybridCLR、Patch UI、构建窗 **不在** 主包。

## 游戏流程

主包 `Cascade.Core`：`GameFlow` / `IGameFlowState` / `IGameFlowQuery`（状态 id 由游戏定义）。  
Indie 示例：`GameEntry` 组状态 → `RunAsync("Main")`；`MainFlowState` / `BattleFlowState` 自管烟测 UI。  
Mobile 热更入口同样可在 `GameLogicEntry` 里 `new GameFlow(...).RunAsync(...)`。

## Mobile vs Indie 对照

| | Mobile | Indie |
|--|--------|-------|
| 资源 | YooAsset 集成 | Addressables 集成 |
| 代码热更 | 有（Starter 内） | 无 |
| 资源热更 | 有（Yoo 方言 API + Starter 编排） | 无 |
| 构建窗 | Starter Editor | 自备 |
| 组合根 | `MobileBootstrapEntry` | `IndieBootstrapEntry` |
| UI 栈（modules.ui） | 默认装 | 不装（烟测用原生 uGUI） |
| 本地化栈（modules.localization） | 默认装 | 默认装 |
| ui.extras | 默认带（目标态） | 默认不带 |
| PC / Steam 壳 | 通常不用 | 可选 Desktop ± Steam ± Glyphs |

## 换资源后端

文档与契约只保证 **load-only** `IResourceService`。默认实现是主包的 `UnityResourcesService`（`Resources/` 文件夹），因此不装集成包也能从零跑通。  
若从 Yoo 换到 Addressables：改组合根工厂与 init options，并删除 Mobile 更新/热更段（或改用 Indie 形状）。更新编排 **不会** 经核心契约自动迁移。

## PC / Steam Integrations

| 包 | 职责 | 依赖 |
|----|------|------|
| **Desktop** `0.2.0` | 本机/漫游设置、JSON 多槽存档、Rebind+冲突、运行时 uGUI、FocusLoss、AudioMixer、光标、日志、退出门、成就接口、云冲突比较 | `com.unity.inputsystem` **1.14.2** |
| **Steam** `0.4.0` | `ISteamClient`、Overlay 暂停、Remote Storage、成就实现、`SteamCloudSaveCoordinator` | Desktop **0.2.0+**；peer Steamworks.NET |
| **InputGlyphs** | `InputActionGlyphBridge` / `ControlSchemeWatcher` | Input System；peer InputGlyphs |

组合根建议：**先注册 Desktop 服务，再挂 Steam 装饰器**。

### Local vs Roaming（硬性）

| 范围 | 文件 | 云同步 |
|------|------|--------|
| **Local** | `cascade-desktop/settings.local.json`（分辨率/全屏/VSync/帧率/画质） | **永不** |
| **Roaming** | `cascade-desktop/settings.roaming.json`（音量/灵敏度/语言） | 可选经 Steam Remote Storage |
| **键位** | `cascade-desktop/input_overrides.json` | 概念可云；**当前仅本机** |

### 增量能力（Desktop 0.2 / Steam 0.4）

- Desktop：`RebindHelper` / 冲突检测、运行时设置/退出/断柄 UI、云存冲突比较、`Samples~/Audio` Mixer
- Steam：`SteamAchievementService`、`SteamCloudSaveCoordinator`
- Indie 接线：[`Starters/Indie/Assets/Scripts/DesktopShell/README.md`](../Starters/Indie/Assets/Scripts/DesktopShell/README.md)

详见各包 README：[`Desktop`](../Integrations/Desktop/README.md) · [`Steam`](../Integrations/Steam/README.md) · [`InputGlyphs`](../Integrations/InputGlyphs/README.md)。
