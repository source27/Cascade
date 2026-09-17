# DesktopMasterMixer

Exposed parameters expected by `AudioMixerVolumes` / `DesktopAudioService`:

| Param | Group | Unit |
|-------|-------|------|
| `MasterVol` | Master | dB |
| `BgmVol` | BGM | dB |
| `SfxVol` | SFX | dB |

## Assign

```csharp
var audio = gameObject.AddComponent<DesktopAudioService>();
audio.Initialize(mixer: yourMixerAsset); // or drag Samples~/Audio/DesktopMasterMixer.mixer
settings.BindAudioMixerVolumes(audio.Volumes);
settings.LoadAndApply();
```

## If the YAML mixer fails to import

Use menu: **Cascade / Desktop / Create Desktop Master Mixer**
(`Editor/CreateDesktopMixer.cs`) — creates a valid mixer under `Assets/CascadeDesktop/` with the same exposed names.
