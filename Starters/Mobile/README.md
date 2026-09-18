# Cascade Mobile Starter

Production starter for mobile live-ops: Cascade + YooAsset + HybridCLR hot-update.

Unity **2022.3.62f3**. Open `Starters/Mobile`.

## Play (EditorSimulate)

1. Open `Assets/Scenes/Bootstrap.unity`
2. Ensure `MobileBootstrapEntry` play mode is EditorSimulate (or Host with DevCDN)
3. Press Play — thin Bootstrap runs, then parked launch flow loads `GameLogic.GameLogicEntry`

## Layout

| Assembly | Role |
|----------|------|
| `Cascade.Mobile.AOT` | `MobileBootstrapEntry`, launch/hot-update pipeline, Patch UI |
| `GameLogic.HotUpdate` | Hot-update game entry (default type name); owns the UI system |
| `Cascade.Mobile.Editor` | Build / hot-update window (`Cascade/构建窗口`) |

Localization is installed by the launch flow (`LocalizationInstaller`); the UI system is created and disposed
by `GameLogicEntry` (`UISystem.CreateAsync` / `Stop`) — neither lives on the host.

## Packages

- `com.source27.cascade` (file)
- `com.source27.cascade.integrations.yooasset` (file)
- `com.source27.cascade.modules.ui` (file) — UI 底座（页面/视图/生成器）
- `com.source27.cascade.modules.uiextras` (file)
- `com.source27.cascade.modules.localization` (file) — 默认本地化运行时 + Sheet→JSON（`Cascade/更新多语言`，首次询问是否创建 settings）
- HybridCLR, YooAsset, UniTask, LitMotion, LoopScroll (vendored under Packages/)
