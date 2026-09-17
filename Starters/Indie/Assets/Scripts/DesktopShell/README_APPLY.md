# Indie Starter 补丁应用说明（中文）

目标目录（示例）：

`C:\Users\source27\Projects\Cascade\Starters\Indie\`

本补丁**只增加**文件，不删除、不覆盖你现有 Indie 工程内容。

---

## 1. 复制 Integrations 包（若尚未落地）

将 zip 中的 `Desktop/`、`Steam/`、`InputGlyphs/` 复制到 Cascade 仓库：

`C:\Users\source27\Projects\Cascade\Integrations\`

---

## 2. 合并 Package 依赖

打开 `Starters/Indie/Packages/manifest.json`，在 `dependencies` 中**追加**（路径按实际相对位置调整）：

见本目录 `MANIFEST_SNIPPET.json` 或 `Packages/manifest.snippet.json`。

最少需要：

- `com.source27.cascade.integrations.desktop`
- （可选）`com.source27.cascade.integrations.steam`
- （可选）`com.source27.cascade.integrations.inputglyphs`
- `com.unity.inputsystem`（Desktop 0.2.0 需要）

Steam 成就 / 云存档还需 peer：`com.rlabrecque.steamworks.net`。

---

## 3. 复制脚本

将下列文件复制到 Indie 工程对应路径（没有文件夹就新建）：

| 补丁内路径 | 复制到 Indie |
|------------|--------------|
| `Assets/Scripts/DesktopShell/IndieDesktopBootstrapHooks.cs` | `Assets/Scripts/DesktopShell/IndieDesktopBootstrapHooks.cs` |
| `Assets/Scripts/DesktopShell/IndieBootSplashFlow.cs` | `Assets/Scripts/DesktopShell/IndieBootSplashFlow.cs` |

---

## 4. 场景接线

1. 启动/Splash 场景空物体挂 `IndieBootSplashFlow`，填写下一场景名（如已有 Bootstrap 场景名）。
2. Bootstrap 场景挂 `IndieDesktopBootstrapHooks`（会 `LoadAndApply` 设置 + FocusLoss；有 Steam 宏时尝试 Init）。
3. 需要退出确认时：`QuitConfirmModal.Create(gate)`；设置面板：`SimpleSettingsPanel.Show(settings)`（默认 F10）。

---

## 5. Steam 可选宏

若已引用 Steam 包，可在 Player / asmdef 定义 `CASCADE_STEAM`，或依赖 Steamworks 后的 `STEAMWORKS_NET`（Steam asmdef versionDefines 会自动加）。

无 Steam 时脚本仍可编译（`#if` 内代码跳过）。

---

## 6. Local vs Roaming（勿改）

- **Local**：分辨率 / 全屏 / vSync / 帧率 / quality → 仅本机 `settings.local.json`
- **Roaming**：音量 / 灵敏度 / 语言等 → `settings.roaming.json`，才可云同步
- 键位 overrides：`cascade-desktop/input_overrides.json`（概念上 roaming-eligible，当前只写本机）

---

## 7. AudioMixer

菜单 **Cascade / Desktop / Create Desktop Master Mixer**，或导入 Desktop 包 `Samples~/Audio/DesktopMasterMixer.mixer`，赋给 `DesktopAudioService`。
