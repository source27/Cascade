# Cascade Integrations — Steam

`com.source27.cascade.integrations.steam` **0.4.0** — **仅 Steam** 能力。  
设置 / 本地存档 / 失焦 / Rebind / UI → **`com.source27.cascade.integrations.desktop` 0.2.0+**（本包依赖 Desktop）。

不捆绑 Steamworks 原生 DLL。速查：[`Docs/INTEGRATION.md`](Docs/INTEGRATION.md)

## 依赖

```json
"com.source27.cascade": "0.1.0",
"com.source27.cascade.integrations.desktop": "0.2.0"
```

Peer：`com.rlabrecque.steamworks.net` → asmdef `versionDefines` → `STEAMWORKS_NET`。

## 本包提供

| 类型 | 作用 |
|------|------|
| `ISteamClient` / `NullSteamClient` / `SteamClientBootstrap` | 客户端抽象 |
| `SteamOverlayPauseDriver` | Overlay 打开时 `timeScale = 0` |
| `SteamRemoteStorageSaveStore` | 装饰 `ISaveService`；Flush 脏键 |
| `SteamRemoteJsonSaveStore` | 装饰 Desktop `IJsonSaveStore`；槽位同步 |
| `SteamRemoteOnlyJsonSaveStore` | 仅 Remote Storage 槽位读写 |
| `SteamCloudSaveCoordinator` | 比较 local/remote → Resolve 写双端 |
| `SteamAchievementService` | 实现 Desktop `IAchievementService` |

**不在本包**：`GameSettings*`、`JsonFileSaveStore`、`FocusLossPauseDriver`、`RebindHelper` — 全部在 Desktop。

## 云同步范围（仅 roaming）

| 数据 | Steam Remote？ |
|------|----------------|
| `settings.local.json`（显示/画质） | **否** |
| `settings.roaming.json` + prefs 键 | 可选 |
| `input_overrides.json` | 概念可；当前未自动同步 |
| JSON 槽位 `slot{N}.json` | 可选 + 冲突 Resolve |
| 成就 SteamUserStats | 是（平台侧） |

## 成就

```csharp
IAchievementService ach = SteamAchievementService.Create();
// STEAMWORKS_NET → SteamUserStats.SetAchievement / StoreStats / GetAchievement
// 否则 → NullAchievementService
ach.Unlock("ACH_FIRST_WIN");
// ResetAll：Steam 无 ClearAll；调试用 Clear(id) 按已知 id 循环
```

接口以 Desktop `IAchievementService` / `NullAchievementService` 为规范。

## 云存档冲突

1. Desktop `CloudSaveConflictResolver`：较新 `updatedUtc` 胜；相等 → Manual；缺一侧 → 用另一侧。
2. `SteamCloudSaveCoordinator`：
   - local = `JsonFileSaveStore`
   - remote = `SteamRemoteOnlyJsonSaveStore`（`cascade-desktop-slot{N}.json`）
   - `TryCompare` → `Resolve(slot, UseLocal|UseRemote)` 将选定信封写到 **两侧**

```csharp
var coord = new SteamCloudSaveCoordinator();
if (coord.TryCompare(0, out var c) && c.suggested == CloudSaveConflictResolution.Manual)
{
    // 弹 UI 让玩家选
}
else
    coord.ResolveSuggested(0);
```

**显示设置永不经 Remote Storage。**

## 组合根示例

先 Desktop，再 Steam：

```csharp
var settings = new GameSettingsService();
gameObject.AddComponent<FocusLossPauseDriver>();
var steam = SteamClientBootstrap.Create();
steam.Init();
var ach = SteamAchievementService.Create();
var coord = new SteamCloudSaveCoordinator();
settings.EnablePrefsMirror(new SteamRemoteStorageSaveStore(innerPrefs));
settings.LoadAndApply();
```

### `steam_appid.txt`

项目根放一行 AppId；测试可用 `480`。

## 文件一览

```
Integrations/Steam/
├── package.json          # 0.4.0；依赖 desktop 0.2.0
├── README.md
├── Docs/INTEGRATION.md
└── Runtime/
    ├── ISteamClient / NullSteamClient / SteamClientBootstrap
    ├── SteamOverlayPauseDriver
    ├── SteamRemoteStorageSaveStore
    ├── Achievements/SteamAchievementService
    └── Save/             # RemoteJson / RemoteOnlyJson / CloudSaveCoordinator
```
