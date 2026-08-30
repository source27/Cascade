# Starters 布局与构建工具归属（更新 ADR 0001）

**Status:** accepted — **updates ADR 0001**（Examples 条款）

仓库保持「`Cascade/` 为包目录、兄弟目录不进包」以防 GUID/导入循环（ADR 0001 核心教训不变）。交付起点目录由 `Examples/` 改为 **`Starters/Mobile`** 与 **`Starters/Indie`**（独立 Unity 工程，`file:` 相对引用 Cascade 与集成/能力包）。Starter 为薄壳：组合根、最小场景/入口、选定依赖；不承载可玩内容切片。

与 YooAsset+HybridCLR 强绑定的 **构建/打包窗口** 只放在 **Mobile Starter**；Addressables 或其它后端的构建工具由对应 Starter/项目自写。菜单文案可以叫 Cascade，程序集边界跟依赖走。
