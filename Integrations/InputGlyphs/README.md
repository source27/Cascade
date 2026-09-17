# Cascade Integrations — InputGlyphs

`com.source27.cascade.integrations.inputglyphs` — 从 d1 `GameInputGlyphBridge` 抽离的**与具体游戏无关**的 Glyph 桥接。

## 安装

1. 引用本包：

```json
"com.source27.cascade.integrations.inputglyphs": "file:../Integrations/InputGlyphs"
```

2. 安装 Unity Input System（本包已依赖 `com.unity.inputsystem` `1.14.2`）。

3. **Peer：InputGlyphs**（不在 `package.json` dependencies 中，请自行安装），例如：

```json
"com.eviltwo.input-glyphs": "https://github.com/eviltwo/InputGlyphs.git?path=InputGlyphs/Assets/InputGlyphs"
```

或 Asset Store 导入。确保程序集名与 asmdef 引用一致：`InputGlyphs`、`InputGlyphs.Display`、`InputGlyphs.Loaders`（若你的 fork 命名不同，请改 `Cascade.Integrations.InputGlyphs.asmdef`）。

4. 场景中放置 InputGlyphs 自带的 `InputGlyphsSetup` 预制体，或在启动时调用：

```csharp
InputGlyphBootstrapGate.EnsureRegistered();
```

（内部反射调用 `InputGlyphs.Display.InputGlyphBootstrap.EnsureRegistered`；类型不存在时安全跳过。）

## 用法

```csharp
using Cascade.Integrations.InputGlyphs;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class GlyphCompositionRoot : MonoBehaviour
{
    [SerializeField] InputActionAsset _actions;

    ControlSchemeWatcher _watcher;
    InputActionGlyphBridge _bridge;

    void Awake()
    {
        InputGlyphBootstrapGate.EnsureRegistered();

        _watcher = new ControlSchemeWatcher();
        _watcher.SchemeChanged += name => Debug.Log($"Scheme: {name}");

        _bridge = new InputActionGlyphBridge(_actions, () => _watcher.CurrentScheme);
        // 注册到 Cascade / 游戏的 IInputGlyphService 槽位
    }

    void OnDestroy()
    {
        _bridge?.Dispose();
        _watcher?.Dispose();
    }
}
```

## 边界

| 做 | 不做 |
| --- | --- |
| `InputActionGlyphBridge` 实现 `InputGlyphs.Display.IInputGlyphService` | 捆绑 Glyph 贴图或 InputGlyphs 本体 |
| `ControlSchemeWatcher` 发出 Gamepad / Keyboard&Mouse scheme 名 | 绑定具体 d1 action 名或 UI |
| 反射 Bootstrap gate | 硬依赖私有 fork API（缺失时 no-op） |

## 注意

若 peer 包程序集名与 `InputGlyphs` / `InputGlyphs.Display` / `InputGlyphs.Loaders` 不一致，请修改 Runtime asmdef 的 `references`。
`IInputGlyphService` / `InputGlyphBootstrap` 若仅存在于你们的 InputGlyphs fork，请确保该 fork 已安装。
