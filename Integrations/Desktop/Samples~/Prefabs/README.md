# Runtime UI (no binary prefabs)

Unity YAML prefabs are fragile without Editor context. Prefer **runtime builders**:

```csharp
using Cascade.Integrations.Desktop;

// Settings
var settings = new GameSettingsService();
settings.LoadAndApply();
SimpleSettingsPanel.Show(settings);

// Quit confirm
var gate = gameObject.AddComponent<QuitConfirmGate>();
QuitConfirmModal.Create(gate);

// Gamepad toast
var watcher = gameObject.AddComponent<GamepadDisconnectWatcher>();
GamepadDisconnectToast.Create(watcher);
```

`DesktopModalCanvas.Ensure()` creates a shared Overlay canvas if needed.
