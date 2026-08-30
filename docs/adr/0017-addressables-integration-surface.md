# Addressables 集成包公开表面

**Status:** accepted — resolves [决策：Addressables 集成包公开表面](https://github.com/source27/Cascade/issues/26)

## 坐标

| | |
|--|--|
| 目录 | `Integrations/Addressables/` |
| UPM | `com.source27.cascade.integrations.addressables` |
| asmdef / 命名空间 | `Cascade.Service.Addressables` |

依赖：`com.source27.cascade`、`com.cysharp.unitask`、**精确 pin** 的 `com.unity.addressables`（与 Unity **2022.3** LTS 官方匹配版本；实现时写入 `package.json`，与 Indie Starter manifest **同一 pin**）。asmdef 引用 `Cascade.Service`、`UniTask`、Addressables 官方程序集（以编译所需为准）。**不**引用 Bootstrap/Core。

## 公开运行时

- `AddressablesResourceService : IResourceService` — **仅** ADR 0015 load-only 成员
- `AddressablesResourceInitOptions : ResourceInitOptions` — **极瘦**（可空/仅 init handle 相关开关）；工程行为靠 AddressableAssetSettings
- Handle 适配 **internal**

**不公开** catalog 更新、下载 size、或任何资源热更封装。需要时 Starter 直接用 Addressables 原生 API。

## 行为约定

- `LoadRawBytesAsync(location)`：`location` 为 address → 加载 `TextAsset` → 返回 bytes；失败抛明确异常（支撑默认本地化）。
- `UnloadUnused`：**no-op**（文档说明依赖 `Release` 引用计数）；不模仿 Yoo 清包缓存。
- 与 Yoo 集成：**命名/目录对称**；**无**更新五件套、**无** PlayMode 枚举、options 更瘦。
