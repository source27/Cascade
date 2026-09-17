# Indie DesktopShell 接线（中文）

仓库内 Integrations（Desktop / Steam / InputGlyphs）与本目录脚本**已落地**。以下按「已在工程里」接线，不要求再从 zip 复制包。

相关包文档：[`Integrations/Desktop`](../../../../../Integrations/Desktop/README.md) · [`Steam`](../../../../../Integrations/Steam/README.md) · [`InputGlyphs`](../../../../../Integrations/InputGlyphs/README.md)

---

## 1. Package 依赖

确认 `Starters/Indie/Packages/manifest.json` 已含（路径按相对位置调整；亦可参考同目录 `MANIFEST_SNIPPET.json` / `Packages/manifest.snippet.json`）：

- `com.source27.cascade.integrations.desktop`（必需）
- `com.unity.inputsystem`（Desktop 0.2.0 需要）
- （可选）`com.source27.cascade.integrations.steam`
- （可选）`com.source27.cascade.integrations.inputglyphs`

Steam 成就 / 云存档还需 peer：`com.rlabrecque.steamworks.net`。

---

## 2. 本目录脚本

| 文件 | 作用 |
|------|------|
| `IndieDesktopBootstrapHooks.cs` | Bootstrap：`LoadAndApply` 设置 + FocusLoss；有 Steam 宏时尝试 Init |
| `IndieBootSplashFlow.cs` | Splash → 下一场景 |

挂载即可，无需再复制。

---

## 3. 场景接线

1. 启动/Splash 场景空物体挂 `IndieBootSplashFlow`，填写下一场景名（如 Bootstrap）。
2. Bootstrap 场景挂 `IndieDesktopBootstrapHooks`。
3. 退出确认：`QuitConfirmModal.Create(gate)`；设置面板：`SimpleSettingsPanel.Show(settings)`（默认 F10）。

---

## 4. Steam 可选宏

已引用 Steam 包时：Player / asmdef 定义 `CASCADE_STEAM`，或依赖 Steamworks 后的 `STEAMWORKS_NET`（Steam asmdef `versionDefines` 会自动加）。

无 Steam 时脚本仍可编译（`#if` 内跳过）。

---

## 5. Local vs Roaming（勿改）

| 范围 | 内容 | 文件 | 云 |
|------|------|------|-----|
| **Local** | 分辨率 / 全屏 / vSync / 帧率 / quality | `settings.local.json` | **永不** |
| **Roaming** | 音量 / 灵敏度 / 语言等 | `settings.roaming.json` | 可选 |
| **键位** | Input overrides | `cascade-desktop/input_overrides.json` | 概念可；当前本机 |

---

## 6. AudioMixer

菜单 **Cascade / Desktop / Create Desktop Master Mixer**，或导入 Desktop `Samples~/Audio/DesktopMasterMixer.mixer`，赋给 `DesktopAudioService`。
