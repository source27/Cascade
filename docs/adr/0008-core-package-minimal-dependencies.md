# 主包依赖最小化：移出 HybridCLR、LitMotion、LoopScrollRect

`com.source27.cascade` 去掉对 HybridCLR、LitMotion、LitMotion.Animation、LoopScrollRect 的 package 依赖，以及 Core/Launcher 中对应的硬引用。

- **HybridCLR**：随热更归属 Mobile Starter（ADR 0007）。
- **LitMotion / LoopScrollRect**：仅服务 `ScaleButton`、`LoopScrollListBinder` 等；迁入可选 UPM **`cascade.ui.extras`**（ADR 0009），需要的 Starter/项目再声明依赖。

**理由：** 主包必须可被「无热更、无循环列表/按钮动效」的 Indie 干净引用；第三方 UI 库是能力选项，不是底座税。
