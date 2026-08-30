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
| `GameLogic.HotUpdate` | Hot-update game entry (default type name) |
| `Cascade.Mobile.Editor` | Build / hot-update window (`Cascade/Mobile/...`) |

## Packages

- `com.source27.cascade` (file)
- `com.source27.cascade.integrations.yooasset` (file)
- `com.source27.cascade.modules.uiextras` (file)
- HybridCLR, YooAsset, UniTask, LitMotion, LoopScroll (vendored under Packages/)
