# Steam 集成速查（0.4.0）

完整说明：[`../README.md`](../README.md)  
**依赖 Desktop 0.2.0+**：设置 / 存档 / 失焦 / Rebind / UI 均在 Desktop。云同步 **仅 roaming**（local 显示设置永不上传）。

## 安装

```json
"com.source27.cascade.integrations.desktop": "file:../Integrations/Desktop",
"com.source27.cascade.integrations.steam": "file:../Integrations/Steam",
"com.unity.inputsystem": "1.14.2"
```

1. Peer：Steamworks.NET；项目根 `steam_appid.txt`
2. 接线顺序：Desktop 服务 → `SteamClientBootstrap` → Overlay → Remote 装饰 → Achievements / `SteamCloudSaveCoordinator`

## 云同步清单

| 数据 | Steam Remote？ |
|------|----------------|
| `settings.local.json` | **否** |
| roaming + prefs | 可选 |
| `input_overrides.json` | 概念可；当前未自动同步 |
| JSON 槽位 | 可选 + Resolve |
| 成就 | 是（平台侧） |

## API 要点

- **客户端**：`SteamClientBootstrap.Create()` → `Init()`
- **成就**：`SteamAchievementService.Create()` → Desktop `IAchievementService`
- **云存**：`SteamCloudSaveCoordinator.TryCompare` / `Resolve`（写双端）
