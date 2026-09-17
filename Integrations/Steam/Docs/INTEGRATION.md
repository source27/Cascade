# Steam 集成速查（0.4.0）

详细见 [README.md](../README.md)。设置 / 存档 / 失焦 / Rebind / UI → **Desktop 0.2.0**。

## 安装

1. manifest：desktop **0.2.0** + steam **0.4.0**
2. Steamworks.NET peer；`steam_appid.txt`
3. Desktop 服务 → Steam bootstrap → Overlay → Remote 装饰 → Achievements / Cloud coordinator

## 云同步清单

| 数据 | 路径/键 | Steam Remote？ |
|------|---------|----------------|
| 显示/画质 | `settings.local.json` | **否** |
| 音量/语言等 | roaming + prefs 键 | 可选 |
| 键位 overrides | `input_overrides.json` | 概念可；当前未自动同步 |
| JSON 槽位 | `slot{N}.json` / `cascade-desktop-slot{N}.json` | 可选 + 冲突 Resolve |
| 成就 | SteamUserStats | 是（平台侧） |

## Achievements

`SteamAchievementService.Create()` → `IAchievementService`。

## 冲突

`SteamCloudSaveCoordinator` + Desktop `CloudSaveConflictResolver`。
