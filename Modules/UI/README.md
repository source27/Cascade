# Cascade UI

Optional **UI stack** for Cascade: page/view base types, UI tree contexts (screens / world widgets),
Roslyn page-registry generator. Main package stays UI-free; install this module when the game wants Cascade's UI stack.

## Install

Monorepo Starter (`Packages/manifest.json`):

```json
"com.source27.cascade.modules.ui": "file:../../../Modules/UI"
```

Git URL:

```
https://github.com/source27/Cascade.git?path=Modules/UI
```

Depends on `com.source27.cascade` (+ uGUI / TMP). Never reverse.

## Assemblies

| Assembly | Platform | Contents |
|----------|----------|----------|
| `Cascade.Modules.UI` | runtime | `UISystem`, `UIBase`, `UIRoot`, `UIRegistry`, `IUISystem`, `WorldUIHost`, `SafeArea`, `UITooltipPlacement`, `UICoordUtility`, `IAtlasSpriteService`/`AtlasSpriteService` … |
| `Cascade.Modules.UI.Editor` | Editor | `UIScriptGenerator` (`Assets/Generate UI Page`), `UIBindingHostEditor`, layout settings |
| `Cascade.Modules.UI.Tests` | Editor | EditMode tests |

Dependencies: `com.source27.cascade` + `com.unity.ugui` / `com.unity.textmeshpro` / `com.unity.2d.sprite`（SpriteAtlas）.

The Roslyn generator ships in this module (`Roslyn/Cascade.SourceGenerator.dll`; sources in `Tools~/Cascade.SourceGenerator/`).
It is inert when the module is absent.

## Atlas sprites

`IAtlasSpriteService`（`AtlasSpriteService`）按名字取 SpriteAtlas 里的 Sprite：索引 `AtlasMapping.bytes`（spriteName → atlasName）与图集句柄都由 `IResourceService` 装载并在服务生命周期内缓存。
它**不随 Bootstrap 默认注册**——需要时在组合根自己登记：

```csharp
protected override void RegisterServices(IServiceRegistry registry)
{
    base.RegisterServices(registry);
    registry.Register<IAtlasSpriteService>(new AtlasSpriteService(
        registry.Get<IResourceService>(), registry.Get<ILogService>()));
}
```

没打包进图集的 Sprite 会返回 null（`TryGetAtlasName` 可用于预判）。

## Ownership

`UISystem` belongs to the **game**, not to the host:

```csharp
var registry = new UIRegistry();
UIRegistryGenerated.RegisterAll(registry);
var ui = await UISystem.CreateAsync(services, registry, "UIRoot", ct); // services = IServiceRegistry
ui.BindPageContext(myGameContext);
ui.SetActiveContext(UIContextId.Main);
```

Dependencies (`IResourceService`, `IUpdateLoop`, `ILogService`) are resolved from the registry passed in.
The caller stores the instance and disposes it on shutdown. The registry has no unregister, so if a project
wants `services.Register<IUISystem>(ui)` for global access it must dispose before re-creating (Mobile retry path).

## Pages

- `[UI(Address = …, Layer = …, Presentation = …)]` on a `UIBase` subclass → `Cascade.Generated.UIRegistryGenerated`.
- Bindings come from the prefab: **Assets → Generate UI Page** (layout override: `ProjectSettings/CascadeUIGeneration.json`).
- `ScaleButton` / `LoopScrollListBinder` live in `com.source27.cascade.modules.uiextras`.
