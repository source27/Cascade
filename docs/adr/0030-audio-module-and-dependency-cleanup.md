# 音频栈出主包（Modules/Audio），主包依赖清零

**Status:** accepted — 修订 [ADR 0027](0027-remove-network-service.md) 所述默认服务集

## 决策

- `IAudioService`（契约）、`AudioService`、`AudioServiceOptions` 与 `AudioServiceTests` 迁入可选模块 `Modules/Audio`（`com.source27.cascade.modules.audio`，程序集/命名空间 `Cascade.Modules.Audio`）。契约随实现进模块（同 [ADR 0026](0026-atlas-sprite-service-belongs-to-ui.md) 的图集先例）。
- `BootstrapBase` 默认集合去掉音频：默认 = 日志 / 资源 / 事件总线 / 存档（+ `IUpdateLoop`）。需要音频的工程在 `RegisterServices` 自行登记：
  `registry.Register<IAudioService>(new AudioService(registry.Get<IResourceService>(), registry.Get<ILogService>()))`
- 主包 `package.json` 依赖清零：去掉 `com.unity.modules.audio`（只有 `AudioService` 用它），并顺手去掉 `com.unity.modules.unitywebrequest`（用它的 GoogleSheet 管线早已随 LocalizationTools 搬进 `Modules/Localization`，主包内**零使用**）。
- `validate-upm.mjs` 把 `modules.audio` 加入「主包禁止依赖」清单，并校验新模块的 package/asmdef；Mobile manifest 与 asmdef（AOT + 热更）、`link.xml`、`AOTGenericReferences` 同步接入该模块。

## 理由

- **重**：`AudioService` 744 行 + options 40 行 + 契约 67 行 + 测试 305 行，占主包实现的 52%（其余 ServiceRegistry 128 / UnityResourcesService 186 / UnityLogService 78 / PlayerPrefsSaveService 38）。
- **策略型**：三通道模型、one-shot 预算与按 key 冷却、池化与 prewarm、Mixer Group 命名路由（`BGM`/`SFX`/`Voice`）——这些是项目口味，不是机制。
- **已经各自为政**：Desktop 集成自带 `DesktopAudioService`（MonoBehaviour + `AudioMixerVolumes`），说明"音频实现因项目而异"是事实；把它当主包默认只是让不玩声音的工程也背一份 744 行。
- 搬完收益是具体的：主包 `package.json` **没有任何依赖**，v0.1 只剩机制（注册表 / 更新循环 / 事件 / 流程）+ 三个 Unity 自带 API 的最基础实现（日志 / 资源 / 存档）。

## Considered options

- 保留在主包但不默认注册（否：依赖与 744 行仍压在主包上，等于没搬）。
- 契约留主包、实现进模块（否：当前只有一份实现、Desktop 的实现并不使用该契约；与图集一致地让契约随实现走）。
- 音频与 UI 合一个模块（否：声音与 UI 无依赖关系，合并会让只要其一的工程背上另一半）。
