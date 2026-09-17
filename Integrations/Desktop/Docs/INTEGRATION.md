# Desktop 集成速查（0.2.0）

完整说明：[`../README.md`](../README.md)

## 安装

```json
"com.source27.cascade.integrations.desktop": "file:../Integrations/Desktop",
"com.unity.inputsystem": "1.14.2"
```

## Local / Roaming

| 文件 | 内容 | 云 |
|------|------|-----|
| `settings.local.json` | 分辨率/全屏/VSync/帧率/画质 | **永不** |
| `settings.roaming.json` | 音量/灵敏度/语言 | 可选（仅 roaming） |
| `input_overrides.json` | 键位 overrides | 概念可；当前本机 |

```csharp
var settings = new GameSettingsService();
settings.LoadAndApply();
settings.Save(); // EnablePrefsMirror 时只镜像 roaming
```

## API 要点

- **设置**：`GameSettingsService` — LoadAndApply / Apply / Save；镜像不含 local
- **存档**：`JsonSaveSlotService` + `TryLoadWithRemote` → `CloudSaveConflictResolution`
- **Rebind / UI**：`RebindHelper.StartRebindWithConflictCheck` · `SimpleSettingsPanel.Show` · `QuitConfirmModal.Create`
