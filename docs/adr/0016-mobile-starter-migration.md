# Mobile Starter 迁移与瘦身策略

**Status:** accepted — resolves [决策：Mobile Starter 迁移与瘦身策略](https://github.com/source27/Cascade/issues/24)

## 迁移

- **`git mv`** `Examples/CascadeExample` → **`Starters/Mobile`**，就地改名/瘦身（保 GUID/meta/HybridCLR 生成物）。
- 实现序：**先**主包可编译（Bootstrap 切割、load-only、去 HybridCLR/LitMotion 等），**再**搬 Mobile 接新 API；避免长期半残 Example。不采用「先改路径仍依赖旧 API」或单巨型混合 PR 为默认。

## 薄壳范围

**保留：** Bootstrap 场景与组合根；PatchWindow；HotUpdate + 默认热更入口约定；HybridCLRGenerate/link.xml/工程设置；Yoo BundleCollector、DevCDN 工具；最小本地化 catalog；构建窗（Starter Editor）；**最小 Home/Detail UI + Gameplay 场景**（冒烟，非可玩切片）。

**去掉：** Example* 命名与 Example 叙事；非冒烟所需的多余演示物。

## 程序集与命名

| 程序集 | 命名空间 | 职责 |
|--------|----------|------|
| `Cascade.Mobile.AOT` | `Cascade.Mobile` | 组合根、`MobileLaunchFlow`（原胖启动流）、CodeLoader/Aot/Patch、Yoo 更新调用 |
| `Cascade.Mobile.HotUpdate` | 默认仍 `GameLogic`（入口类型 `GameLogic.GameLogicEntry`，程序集名 `GameLogic.HotUpdate`） | 热更逻辑；本轮不改 HybridCLR/打包默认字符串 |
| `Cascade.Mobile.Editor` | `Cascade.Mobile.Editor` | 构建/打包窗 |

- 组合根类型：**`MobileBootstrapEntry`**（`: BootstrapBase`），场景名 `Bootstrap`。
- 热更入口签名保持 ADR 0014 约定；配置默认值留在 Mobile。

## 与薄 Bootstrap 衔接

```
MobileBootstrapEntry
  CreateResourceService → YooAssetResourceService（保留具体引用）
  CreateResourceInitOptions → Yoo options（Mobile 自有 play mode 字段）
  RunGameAsync → 更新（Yoo 具体 API）+ Patch UI + CodeLoader + 热更 Start(host)
```

## manifest（目标态）

必有：`com.source27.cascade`、`com.source27.cascade.integrations.yooasset`、YooAsset、HybridCLR、UniTask、ugui/TMP。  
**默认带** `com.source27.cascade.modules.uiextras`（主包去掉 LitMotion/LoopScroll 之后）。
