# Cascade Audio

Optional UPM module: Cascade's default audio stack. Main package ships no audio — install this when the game plays sound.

## Install

Monorepo Starter (`Packages/manifest.json`):

```json
"com.source27.cascade.modules.audio": "file:../../../Modules/Audio"
```

Git URL:

```
https://github.com/source27/Cascade.git?path=Modules/Audio
```

Depends on `com.source27.cascade` + `com.unity.modules.audio`. Never reverse.

## Assemblies

| Assembly | Platform | Contents |
|----------|----------|----------|
| `Cascade.Modules.Audio` | runtime | `IAudioService`（契约）、`AudioService`、`AudioServiceOptions` |
| `Cascade.Modules.Audio.Tests` | Editor | EditMode 测试（假 `IResourceService` + 可控时钟） |

## Wiring

Module 不做任何自动注册——需要时在组合根登记（此时你自己的 log/resource 已注册）：

```csharp
protected override void RegisterServices(IServiceRegistry registry)
{
    registry.Register<ILogService>(new UnityLogService { Enabled = true, MinimumLevel = LogLevel.Info });
    registry.Register<IResourceService>(new YooAssetResourceService(options));
    registry.Register<IAudioService>(new AudioService(
        registry.Get<IResourceService>(), registry.Get<ILogService>()));
}
```

`AudioService` 也可用 `AudioServiceOptions` 调 one-shot 预算/池/Mixer Group 路由，或用 `Configure(options)` 事后替换部分设置。

## 能力

- 三通道：BGM（`PlayBgmAsync` / `PauseBgm` / `ResumeBgm` / `StopBgm`）、SFX（`PlayOneShotAsync` / `PlayOneShotAtAsync`，含并发上限、按 key 实例上限与冷却、池耗尽丢弃并返回 0）、Voice（`PlayVoiceAsync` / `StopVoice`）。
- `BindMixerAsync(location)` 按资源位置加载 `AudioMixer`，路由到名为 `BGM` / `SFX` / `Voice` 的 Group（缺失则不路由）。
- `PreloadAsync` / `Release` / `ReleaseAll`、`SetVolume`（主音量与通道音量）、`MasterVolume` / `GetVolume` 供设置界面回显。
