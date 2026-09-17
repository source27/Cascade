# Cascade Integrations — Desktop

`com.source27.cascade.integrations.desktop` **0.2.0** — PC 壳（设置 / 存档 / 重绑定 / 运行时 UI / 失焦暂停 / 音频与云冲突辅助）。**与 Steam 无关**；Steam 包依赖本包。

| | |
|--|--|
| 命名空间 | `Cascade.Integrations.Desktop` |
| Runtime 程序集 | `Cascade.Integrations.Desktop`（`Cascade.Service` + `Unity.InputSystem`） |
| Editor | `Cascade.Integrations.Desktop.Editor`（可选创建 Mixer） |
| 依赖 | `com.unity.inputsystem` **1.14.2**（package.json） |

速查：[`Docs/INTEGRATION.md`](Docs/INTEGRATION.md)

## Desktop vs Steam

| 包 | 职责 |
|----|------|
| **Desktop** | 本机/漫游设置、JSON 多槽存档、Rebind+冲突、运行时 uGUI、FocusLoss、AudioMixer 音量、光标、日志、退出门、成就接口、云冲突比较 |
| **Steam** | `ISteamClient`、Overlay 暂停、Remote Storage、成就实现、`SteamCloudSaveCoordinator` |

组合根：**先注册 Desktop，再挂 Steam 装饰器**。

## 设置：Local vs Roaming

**硬性**：显示/画质只落本机；音量与可移植偏好才可云同步。

| 范围 | 文件 | 字段 | 云同步 |
|------|------|------|--------|
| **LOCAL** | `persistentDataPath/cascade-desktop/settings.local.json` | 分辨率、fullscreenMode、vSync、targetFrameRate、qualityLevel | **禁止** |
| **ROAMING** | `…/settings.roaming.json` | master/bgm/sfx、mouseSensitivity、gamepadDeadzone、language | 可选：`EnablePrefsMirror` 仅 roaming |
| **键位** | `…/input_overrides.json` | Input System binding overrides | 概念可云；**当前仅本机** |

类型：`LocalDisplaySettings` / `RoamingGameSettings` / `GameSettingsModel` / `GameSettingsStore` / `GameSettingsService` / `GameSettingsApplier`。

```csharp
var settings = new GameSettingsService();
settings.LoadAndApply();
settings.Apply();
settings.Save(); // local + roaming；镜像只含 roaming
```

## 输入重绑定

```csharp
var rebind = new RebindHelper(actionAsset);
rebind.LoadOverrides();
rebind.StartRebindWithConflictCheck(action, bindingIndex,
    onComplete: path => { },
    onCancel: () => { },
    onConflict: path => { /* 已自动撤销 */ });
```

- `RebindConflictDetector` — 同 Map 路径冲突
- Overrides：`cascade-desktop/input_overrides.json`（勿写入显示设置）

## 运行时 UI（无二进制 Prefab）

| API | 作用 |
|-----|------|
| `DesktopModalCanvas.Ensure()` | Overlay Canvas |
| `QuitConfirmModal.Create(gate)` | Yes/No + `QuitConfirmGate` |
| `GamepadDisconnectToast.Create(watcher)` | IToastHook / 事件 |
| `SimpleSettingsPanel.Show(service)` | Local / Roaming 分区；Save/Apply/Reset |

说明：`Samples~/Prefabs/README.md`。

## AudioMixer

- Exposed：`MasterVol` / `BgmVol` / `SfxVol`（dB）→ `AudioMixerVolumes`
- 样例：`Samples~/Audio/DesktopMasterMixer.mixer`
- 菜单：`Cascade/Desktop/Create Desktop Master Mixer`
- `DesktopAudioService.Initialize(mixer: …)` → `BindAudioMixerVolumes`

## JSON 多槽存档 + 云冲突

路径：`cascade-desktop/saves/slot{N}.json`  
API：`IJsonSaveStore` / `JsonFileSaveStore` / `JsonSaveEnvelope` / `JsonSaveSlotService`

| 类型 | 作用 |
|------|------|
| `CloudSaveConflict` / `CloudSaveConflictResolution` | None / UseLocal / UseRemote / Manual |
| `CloudSaveConflictResolver.Compare` | 较新 `updatedUtc` 胜；相等 → Manual；缺一侧 → 另一侧 |
| `JsonSaveSlotService.TryLoadWithRemote(...)` | local + remote → conflict |

Steam 写回：`SteamCloudSaveCoordinator`（见 Steam README）。

## 其它 Runtime API

| 区域 | 类型 |
|------|------|
| Audio | `AudioMixerVolumes`, `DesktopAudioService` |
| Boot | `SceneFader` |
| State | `CursorLockService`, `SimpleStateMachine`, `FocusLossPauseDriver` |
| Logging | `FileLogger` |
| Input | `GamepadDisconnectWatcher`, `RebindHelper`, `RebindConflictDetector` |
| UI | `QuitConfirmGate`, `VersionLabelBinder`, runtime builders |
| Achievements | `IAchievementService`, `NullAchievementService`（Steam 实现见 Steam 包） |
| Localization | `SimpleStringTable` |

## 安装

```json
"com.source27.cascade.integrations.desktop": "file:../Integrations/Desktop",
"com.unity.inputsystem": "1.14.2"
```

不依赖 UniTask；不依赖 Steamworks。

## 文件树

```
Integrations/Desktop/
├── package.json          # 0.2.0
├── README.md
├── Docs/INTEGRATION.md
├── Editor/               # CreateDesktopMixer
├── Samples~/Audio/  Prefabs/
└── Runtime/              # Settings / Save / Audio / Boot / State /
                          # Logging / Input / UI / Achievements / Localization
```
