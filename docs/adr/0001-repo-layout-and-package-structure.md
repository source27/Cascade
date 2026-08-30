# 仓库布局与包结构

**Status:** accepted — Starter 路径与构建工具归属见 [ADR 0012](0012-starters-layout-and-build-tools.md)（`Examples/` → `Starters/`）

UPM 包 `com.source27.cascade` 位于仓库子目录 **`Cascade/`**（`package.json` 在 `Cascade/` 内，git URL 安装必须带 `?path=Cascade`）；示例工程在仓库根的 `Examples/CascadeExample/`（独立 Unity 工程，manifest 以 `file:../../../Cascade` 相对引用——Unity 的 `file:` 按 manifest 所在目录解析）；资源提供者集成子包在 `Integrations/YooAsset/`（`?path=Integrations/YooAsset` 安装）；源码生成器源码在包内 `Tools~/`（波浪号目录，Unity 不导入）而编译产物在 `Cascade/Roslyn/`（无 asmdef，按 Unity 官方规则作用于全工程程序集）。

## 为什么包不在仓库根（实测教训）

最初布局是「仓库根即包 + `Examples/` 在包内」：Unity 会把包内所有非 `~` 目录当包资产导入，嵌套的示例工程因此被同时当作「工程资产」和「包资产」处理 → 同一批文件 GUID 冲突 → Unity 改写 meta → 文件变化触发再导入 → **导入无限循环**（`Importing (iteration N)` 迭代数永涨，日志实锤 `GUID conflicts ... Build asset version error`）；`Integrations/YooAsset` 在包内时，其 asmdef 引用未安装的 YooAsset 还会让主包安装即报编译错。

**结论**：包内容以外的目录必须放在包目录之外（兄弟目录），或（仅在包内时）使用 `~` 后缀。当前布局把包收进 `Cascade/` 子目录，`Examples/`/`Integrations/` 成为仓库根兄弟——两者都不是包内容，天然免导入；`~` 约定只保留给包内非导入内容（`Cascade/Tools~`）。

## Considered Options

- 仓库根即包 + `Examples~` 波浪号：可用但示例工程路径带 `~` 观感差，且 `?path` 语义不如目录名直观——被用户否决。
- 示例工程独立仓库：排除——单仓库便于同步与联调。
- 生成器独立包：排除——多包安装需多个 `?path`，收益不抵复杂度。
