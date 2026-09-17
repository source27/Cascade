# Desktop 集成速查（0.2.0）

## Local / Roaming 拆分

```
GameSettingsService.Load()
  → settings.local.json + settings.roaming.json → Current
  →（可选）prefs 仅补齐 roaming

GameSettingsService.Save()
  → 双文件；EnablePrefsMirror 时只上传 roaming JSON
```

**Steam 不得 FileWrite 显示设置。**

键位 overrides：`cascade-desktop/input_overrides.json` — roaming-eligible 概念，当前本机；**不要**把 display 塞进 overrides。

## Rebind

`RebindHelper` / `RebindConflictDetector` / `StartRebindWithConflictCheck`。

## Runtime UI

`DesktopModalCanvas` · `QuitConfirmModal` · `GamepadDisconnectToast` · `SimpleSettingsPanel.Show(service)`  
（面板标签区分 LOCAL / ROAMING）

## Mixer

Exposed：`MasterVol` `BgmVol` `SfxVol` → `AudioMixerVolumes`。  
Samples 或菜单 `Cascade/Desktop/Create Desktop Master Mixer`。

## 云存档冲突

```csharp
slots.TryLoadWithRemote(localStore, remoteStore, slot, out var conflict);
// conflict.suggested: None | UseLocal | UseRemote | Manual
```

Steam 侧用 `SteamCloudSaveCoordinator.Resolve(slot, UseLocal|UseRemote)` 写回两侧。

## 与 Steam 组合

1. Desktop：`GameSettingsService`、`JsonSaveSlotService`、`FocusLossPauseDriver`、`RebindHelper`
2. Steam：`SteamClientBootstrap`、`SteamOverlayPauseDriver`、`SteamRemoteStorageSaveStore`、`SteamRemoteJsonSaveStore`、`SteamAchievementService.Create()`、`SteamCloudSaveCoordinator`
