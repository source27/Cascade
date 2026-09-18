# 图集精灵服务归 Modules/UI，主包去掉 2d.sprite 依赖

**Status:** accepted — 修订 [ADR 0025](0025-default-resource-provider-and-hooks.md) 中「图集 = `AtlasSpriteService` 主包默认」一项

## 决策

- `IAtlasSpriteService`（契约）与 `AtlasSpriteService`（实现）从 `Cascade.Service` 迁到 `Modules/UI`（命名空间 `Cascade.Modules.UI`）。
- `BootstrapBase` 删除 `CreateAtlasSpriteService` 钩子与注册：它**不再是默认服务**，需要的项目在 `RegisterServices` 里自行 `registry.Register<IAtlasSpriteService>(new AtlasSpriteService(resources, log))`。
- 主包 `package.json` 去掉 `com.unity.2d.sprite`；`Modules/UI` 接管该依赖（SpriteAtlas 资产由 2D Sprite 包提供创作面）。
- `scripts/validate-upm.mjs` 把 `2d.sprite` 加入「主包禁止依赖」清单。

## 理由

SpriteAtlas → Sprite 的查找是**给 UI 用的**（uGUI Image 的精灵来源），与 `Modules/UI` 的定位一致；放在主包会让「不做 UI 的工程」也背上 2D Sprite 依赖与一套 UI 约定（`AtlasMapping` 索引）。契约随实现进模块也符合 `IUISystem`/`UIRegistry` 的先例——模块自有契约、主包不反向依赖。

## Considered options

- **契约留 `Cascade.Service`、实现进模块**（否：目前只有这一份实现、只服务 UI，留个无消费者的主包契约与 ADR 0011/0024 的取向相反；将来若真出现第二个 atlas 语义再提升）。
- **直接删除**（否：作者明确要把它放进 UI 模块，且它是可用的 UI 能力，只是当前无人接线）。
- **留在主包但不在 Bootstrap 注册**（否：依赖与约定仍压在主包上，等于没搬）。
