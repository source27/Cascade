# Launcher / Bootstrap 热更与补丁耦合面

**Ticket:** [#18](https://github.com/source27/Cascade/issues/18)  
**Sources:** `Cascade/Runtime/Cascade.Launcher/*`, `Cascade/Runtime/Cascade.Launcher/Cascade.Launcher.asmdef`, `Examples/CascadeExample/Assets/Scripts/AOT/ExampleBootstrapEntry.cs`, `CONTEXT.md`, [ADR 0007](../adr/0007-hot-update-belongs-to-mobile-starter.md)

## 结论（给决策票）

当前 `Cascade.Launcher` 是 **胖启动器**：组合根 + 资源版本/下载编排 + HybridCLR 代码加载 + Patch UI 绑在同一程序集。按 ADR 0007 / CONTEXT，主包 Bootstrap 应只到 **资源 Initialize + 交付 `IGameHost`**；其余迁 **Mobile Starter**。

## 程序集依赖

| 引用 | 含义 |
|------|------|
| `Cascade.Service`, `Cascade.Core`, `Cascade.Module` | 底座 |
| `HybridCLR.Runtime` | **热更耦合** — 主包必须去掉 |
| `UniTask`, `UnityEngine.UI` | UI 补丁窗需要 uGUI |

## 类型归属建议

| 类型 | 路径 | 标签 | 建议归属 |
|------|------|------|----------|
| `BootstrapEntry` | `BootstrapEntry.cs` | core + 现绑 PatchWindow | **主包保留壳**：虚钩子 + 注册默认服务 + Host；去掉对 `PatchWindow`/`LauncherFlow` 全热更流水线的硬绑 |
| `GameHost` | `GameHost.cs` | core | **主包** |
| `BootstrapConfiguration` | `BootstrapConfiguration.cs` | 混 | **拆**：热更 DLL/入口/AOT 元数据字段 → Starter；资源 options 槽位可留主包 |
| `LauncherFlow` | `LauncherFlow.cs` | update + hybridclr + patch-ui + code-load | **Mobile Starter**（或拆成 Starter 流水线；主包最多留「init 资源 → 调主逻辑入口」的瘦 flow） |
| `CodeLoader` | `CodeLoader.cs` | hybridclr + code-load | **Mobile Starter** |
| `AotMetadataCatalog` | `AotMetadataCatalog.cs` | hybridclr | **Mobile Starter** |
| `ILauncherView` | `ILauncherView.cs` | patch-ui + download | **Mobile Starter**（下载确认/进度是资源热更 UI） |
| `PatchWindow` | `PatchWindow.cs` | patch-ui | **Mobile Starter**（场景/预制体一并） |
| `LauncherText` | `LauncherText.cs` | patch-ui | **Mobile Starter**（补丁文案表） |
| `LauncherTypes` 枚举 | `LauncherTypes.cs` | 混 | **拆**：`BootstrapEnvironment` / 通用失败类型可留；`BootstrapPlayMode` Host 下载语义、`BootstrapAssemblyLoadMode`、热更相关 `LauncherStage` → Starter |

示例侧：`ExampleBootstrapEntry` 已是 YooAsset 组合根，改造后应成为 Mobile Starter 组合根并 **吞下** 迁出的热更/补丁代码。

## `LauncherFlow` 步骤（现状）

顺序来自 `LauncherFlow.cs`：

1. Install（noop）
2. **InitializeResource** — `IResourceService.InitializeAsync` → **可留主包 Bootstrap**
3. **CheckUpdate** — `RequestVersionAsync` + `UpdateManifestAsync` → **Starter**
4. **DownloadPatch**（Host）— `PrepareDownload` / 确认 / `DownloadAsync` / `ClearUnusedCacheAsync` → **Starter**；EditorSimulate/Builtin 仅状态文案
5. InitializeLocalization — 可留主包（与热更无关）
6. **LoadAOTMetadata** — HybridCLR → **Starter**
7. **LoadGameLogicAssembly** — `CodeLoader` → **Starter**
8. **LaunchGame** — 反射 `Start(IGameHost, CT)` → **Starter 约定**；Indie 改为同进程直接调用
9. Completed + HideWindow → **Starter**（有 Patch UI 时）

## 主包 Bootstrap 目标形状（研究建议，非最终决策）

```
BootstrapEntry
  → config（无热更字段）
  → RegisterServices + CreateResourceService/Options
  → UpdateLoop + GameHost
  → resources.InitializeAsync
  → localization.InitializeAsync（可选）
  → 交给「主逻辑入口」钩子（虚方法 / 委托；默认 no-op 或文档约定由 Starter override）
```

## 测试牵连

- `Cascade/Tests/Cascade.Tests/BootstrapConfigurationTests.cs` — 热更默认字段
- `Cascade/Tests/Cascade.Tests/LauncherTextTests.cs` — 随 `LauncherText` 迁出或删除

## 非目标

本笔记不决定入口方法签名、是否保留名为 `LauncherFlow` 的类型、或 Mobile 目录布局（见决策票 Bootstrap / Mobile）。
