# 如何组装 Cascade

## 包一览

| 路径 | UPM name | 何时安装 |
|------|----------|----------|
| `Cascade/` | `com.source27.cascade` | 总是 |
| `Integrations/YooAsset/` | `com.source27.cascade.integrations.yooasset` | 手游 / Yoo 资源管线 |
| `Integrations/Addressables/` | `com.source27.cascade.integrations.addressables` | 独立游戏 / Addressables |
| `Modules/UiExtras/` | `com.source27.cascade.modules.uiextras` | 需要 ScaleButton / LoopScroll 时 |
| `Modules/LocalizationTools/` | `com.source27.cascade.modules.localizationtools` | 需要 Google Sheet→JSON 等本地化作者工具时 |

主包程序集：`Cascade.Service`、`Cascade.Core`、`Cascade.Bootstrap`、`Cascade.Editor`、`Cascade.Tests`。

依赖方向：Integrations / Modules → 主包；Starter → 任选。

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
"com.source27.cascade.modules.uiextras": "https://github.com/source27/Cascade.git?path=Modules/UiExtras",
"com.source27.cascade.modules.localizationtools": "https://github.com/source27/Cascade.git?path=Modules/LocalizationTools"
```

Addressables 集成 pin `com.unity.addressables` **1.21.19**（与 Indie Starter 一致）。

### 第三方依赖（需自行安装）

`package.json` 依赖值只能是 SemVer；git URL 只允许出现在工程 `manifest.json`。第三方依赖须在工程里先装好（与仓库 pin 的提交一致，或经 OpenUPM 作用域 registry 装同名版本）：

| 依赖包 | 版本 | 需要它的包 |
|--------|------|-----------|
| com.cysharp.unitask | 2.5.11 | 主包 / YooAsset / Addressables |
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

先装齐所需第三方依赖，再添加 Cascade 各包。

主包 **不再** 依赖 HybridCLR、LitMotion、LoopScrollRect。

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

## 从零组装（不 fork Starter）

1. 空工程安装主包 + 选定集成包  
2. 场景挂载继承 `Cascade.Bootstrap.BootstrapEntry` 的组合根  
3. Override：

```csharp
protected override IResourceService CreateResourceService() =>
    new YooAssetResourceService(); // 或 AddressablesResourceService

protected override ResourceInitOptions CreateResourceInitOptions() =>
    new YooAssetResourceInitOptions(...); // 或 AddressablesResourceInitOptions

protected override async UniTask RunGameAsync(IGameHost host, CancellationToken ct)
{
    // Mobile: 资源更新（Yoo 具体 API）+ CodeLoader + 热更 Start
    // Indie: await GameEntry.Start(host, ct);
}
```

4. 需要 UI 动效/循环列表时再装 `modules.uiextras`  

## 默认流水线止点

Bootstrap **只到** 资源 init（+ 可选本地化 init）和 `RunGameAsync`。  
资源 **更新**、HybridCLR、Patch UI、构建窗 **不在** 主包。

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
| ui.extras | 默认带（目标态） | 默认不带 |

## 换资源后端

文档与契约只保证 **load-only** `IResourceService`。  
若从 Yoo 换到 Addressables：改组合根工厂与 init options，并删除 Mobile 更新/热更段（或改用 Indie 形状）。更新编排 **不会** 经核心契约自动迁移。
