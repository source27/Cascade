# Cascade

Unity **能力库**（`com.source27.cascade`）+ 可 fork **Starter**。  
薄 Bootstrap、提供者无关服务层、Core UI/事件/生命周期、Roslyn UI 生成器。  
**不管** HybridCLR 热更编排与资源下载流水线——那些在 Mobile Starter。

术语：[`CONTEXT.md`](CONTEXT.md) · 决策：[`docs/adr/`](docs/adr/) · 指南：[`docs/philosophy.md`](docs/philosophy.md) · [`docs/assembly.md`](docs/assembly.md) · [`docs/coding.md`](docs/coding.md)

## 仓库结构

```
仓库根
├── Cascade/                         UPM 主包 com.source27.cascade（?path=Cascade）
│   ├── Runtime/Cascade.Service/     契约 + 默认实现
│   ├── Runtime/Cascade.Core/        UI / 事件 / 更新循环 / 生命周期
│   ├── Runtime/Cascade.Bootstrap/   薄启动编排 + 组合根基类
│   ├── Editor/  Tests/  Roslyn/  Tools~/
│   └── package.json
├── Integrations/
│   ├── YooAsset/                    可选资源集成（含更新 API 在具体类型上）
│   ├── Addressables/                可选资源集成（load-only）
│   ├── Desktop/                     PC 壳：设置 / 存档 / 重绑定 / 运行时 UI
│   ├── Steam/                       Steam 能力（依赖 Desktop）
│   └── InputGlyphs/                 手柄/键鼠 Glyph 桥接
├── Modules/
│   ├── UiExtras/                    可选 UI（ScaleButton / LoopScroll）
│   └── LocalizationTools/           可选本地化作者工具（Sheet→JSON）
├── Starters/
│   ├── Mobile/                      手游生产起点（Yoo + HybridCLR + 构建窗）
│   └── Indie/                       独立游戏起点（Addressables，无热更）
├── CONTEXT.md
└── docs/
```

依赖方向：`Integrations/*`、`Modules/*` → 主包；Starter → 主包 + 选定子包。主包不反向依赖。

## 安装主包

Unity **2022.3+**。Package Manager → git URL：

```
https://github.com/source27/Cascade.git?path=Cascade
```

`?path=Cascade` **必须**（包不在仓库根）。

本地 monorepo（路径相对你的 `Packages/manifest.json`）：

```json
"com.source27.cascade": "file:../../../Cascade"
```

### 第三方 peer 依赖（工程 manifest 提供）

UPM `package.json` **只**允许依赖 Unity 注册表包（`com.unity.*`）和本仓库包（`com.source27.cascade*`）。  
不能把 UniTask 等写成 SemVer 硬依赖——它们不在 Unity 注册表，`Add package from git URL` 会直接报 `cannot be found`。  
git/file URL 只属于**工程** `Packages/manifest.json`（或 OpenUPM scoped registry）。

asmdef 仍引用这些程序集；工程必须自行装齐，否则编译失败。Starter 已配好。

| Peer 包 | 版本 pin | 需要它的包 |
|--------|----------|-----------|
| com.cysharp.unitask | 2.5.11 | 主包 / YooAsset / Addressables |
| com.annulusgames.lit-motion | 2.0.2 | UiExtras |
| com.annulusgames.lit-motion.animation | 2.0.2 | UiExtras |
| me.qiankanglai.loopscrollrect | 1.1.5 | UiExtras |
| com.tuyoogame.yooasset | 3.0.5 | YooAsset 集成 |

推荐在工程 `manifest.json` 写入（与仓库 pin 的提交一致）：

```json
"com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2e993ff18f28c931602a07292df0b0804eebef99",
"com.annulusgames.lit-motion": "https://github.com/AnnulusGames/LitMotion.git?path=src/LitMotion/Assets/LitMotion#0b4c588ee75a07198841d92aab653e6b39445089",
"com.annulusgames.lit-motion.animation": "https://github.com/AnnulusGames/LitMotion.git?path=src/LitMotion/Assets/LitMotion.Animation#0b4c588ee75a07198841d92aab653e6b39445089",
"me.qiankanglai.loopscrollrect": "https://github.com/qiankanglai/LoopScrollRect.git#a74a705e1c9d0f73ea1a441dabb23b22f3283071",
"com.tuyoogame.yooasset": "https://github.com/tuyoogame/YooAsset.git?path=Assets/YooAsset#94422fc41491228eed0999ce4845d7b23ee2b8ae"
```

也可经 OpenUPM 作用域 registry 装**同名同版本**。

### 主包 UPM 依赖（注册表可解析）

| 包 | 说明 |
|----|------|
| com.unity.ugui / TMP | UI |
| Unity 模块 | audio、webrequest、2d.sprite |

**编译期 peer：** `com.cysharp.unitask`（见上，不进 `package.json`）。  
**不含** HybridCLR、LitMotion、LoopScrollRect、YooAsset（后三者按需装 Modules/Integrations）。

## 快速开始

### 手游

1. Unity **2022.3.62f3** 打开 [`Starters/Mobile`](Starters/Mobile)  
2. `Assets/Scenes/Bootstrap.unity` → Play  
3. 组合根：`MobileBootstrapEntry`；构建菜单：**Cascade/构建窗口**
4. 说明：[`Starters/Mobile/README.md`](Starters/Mobile/README.md)

### 独立游戏

1. 打开 [`Starters/Indie`](Starters/Indie)  
2. `Assets/Scenes/Bootstrap.unity` → Play  
3. 组合根：`IndieBootstrapEntry` → `GameEntry`  
4. 说明：[`Starters/Indie/README.md`](Starters/Indie/README.md)

从零组装、换后端：见 [`docs/assembly.md`](docs/assembly.md)。

## 资源层

`IResourceService` **仅** init/load/unload（无版本/下载方法）。

- **YooAsset**：集成包；更新 API 在 `YooAssetResourceService` 具体类型上，由 Mobile `RunGameAsync` 调用  
- **Addressables**：集成包 load-only；`LoadRawBytesAsync` = TextAsset address  

## 热更（仅 Mobile Starter）

主包 **无** HybridCLR 依赖。热更 dll 加载、AOT 元数据、Patch UI、构建窗均在 Mobile。  
热更入口约定（Starter 侧）：`public static UniTask<string> Start(IGameHost, CancellationToken)`，默认 `GameLogic.GameLogicEntry`。

## PC / Steam 壳

可选 Integrations，面向桌面独立游戏（Indie Starter 已预留接线）：

| 包 | 说明 |
|----|------|
| [`Integrations/Desktop`](Integrations/Desktop/README.md) | 本机/漫游设置、JSON 多槽存档、Rebind、运行时 UI、失焦暂停等（**与 Steam 无关**） |
| [`Integrations/Steam`](Integrations/Steam/README.md) | Overlay / Remote Storage / 成就 / 云存协调（**依赖 Desktop**） |
| [`Integrations/InputGlyphs`](Integrations/InputGlyphs/README.md) | 手柄/键鼠 Glyph 桥接 |

Indie 接线步骤：[`Starters/Indie/Assets/Scripts/DesktopShell/README.md`](Starters/Indie/Assets/Scripts/DesktopShell/README.md)。

硬性规则：**local 显示/画质永不云同步**；仅 roaming prefs + JSON 槽位可经 Steam Remote Storage。

## 文档索引

| 文档 | 内容 |
|------|------|
| [`CONTEXT.md`](CONTEXT.md) | 领域词汇 |
| [`docs/philosophy.md`](docs/philosophy.md) | 设计思想 |
| [`docs/assembly.md`](docs/assembly.md) | 选包与组装 |
| [`docs/coding.md`](docs/coding.md) | 组合根 / 服务 / 资源 / UI / 事件 |
| [`docs/adr/`](docs/adr/) | ADR（含 0006–0019 结构改造） |
| Starter READMEs | 各工程 Play / 打包步骤 |
| [`Integrations/Desktop/README.md`](Integrations/Desktop/README.md) | PC 壳：设置 / 存档 / Rebind / UI |
| [`Integrations/Steam/README.md`](Integrations/Steam/README.md) | Steam（依赖 Desktop） |
| [`Integrations/InputGlyphs/README.md`](Integrations/InputGlyphs/README.md) | Glyph 桥接 |
| [`Starters/Indie/.../DesktopShell/README.md`](Starters/Indie/Assets/Scripts/DesktopShell/README.md) | Indie PC / Steam 接线 |

## 状态

结构改造（双交付物、薄 Bootstrap、load-only 资源契约、双 Starter）已落地。  
开新项目：fork **Mobile** 或 **Indie**，不要再找 Example。
