# 内置包目录 + UPM Client 的「集成与模块」窗口

**Status:** accepted

主包 `Cascade.Editor` 新增 `PackageManagerWindow`（菜单 **Cascade → 集成与模块**）：列出所有可选模块与集成、显示是否已装、一键安装/移除、复制 manifest 片段。

## 决策

- **安装机制**：用官方 API `UnityEditor.PackageManager.Client.Add/Remove/List`，**不**手改 `Packages/manifest.json`（手改无解析回执、易写坏、且需要等 Unity 重载）。
- **目录来源**：包内嵌 `Cascade/Editor/Cascade.Editor/PackageCatalogue.json`。原因：用 git URL 装 Cascade 的工程只拿到 `?path=Cascade` 子树，`Modules/`、`Integrations/` 在磁盘上不存在，无法"扫目录"得到清单。
- **漂移守卫**：`scripts/validate-upm.mjs` 校验目录与条目**双向**一一对应（有目录没条目、有条目没目录都报错），并校验条目 `name` 与目标 `package.json` 一致；`PackageCatalogueTests` 在 EditMode 里复查同一件事。
- **安装引用随主包来源切换**：
  - 本地 monorepo 检出（`Modules/`、`Integrations/` 与主包同级）→ 相对 `file:` 路径（与 Starter manifest 同形），`Client.Add` 用绝对 `file:` 路径以免歧义；
  - git 安装 → `<repo>?path=<模块路径>`，并 **pin 到主包自身的 revision**（`PackageInfo.git.revision/hash`）；仓库 URL 优先取主包 git 信息（fork 也能用），取不到才回退 JSON 里的 `repository`。
- **peer 只展示、不代装**：LitMotion / LoopScrollRect / YooAsset / InputGlyphs 等第三方包的 URL 只做展示与"复制 manifest 片段"，第三方依赖由项目自己决定版本与来源。
- **零依赖**：窗口只用 `UnityEditor` + `System`，异步用 `EditorApplication.update` 轮询 `Request.IsCompleted`，不给主包添任何 package 或 asmdef 引用。

## Considered options

- 目录硬编码在 C# 里：否——新增模块要改代码；JSON 便于维护且能被 CI 与测试校验。
- 运行时扫仓库目录：否——git 安装时不存在这些目录（见上）。
- 直接写 `manifest.json`：否——没有解析回执，失败态不可知。
- 自动安装第三方 peer：否——等于替项目选外部依赖版本；只给片段与 URL。
- 把窗口放进某个 Starter：否——每个 fork 都要重做，且它本就该知道全部可选包。

## 已知取舍

内嵌目录会随仓库演进而漂移——这是这个方案唯一的新增维护点，因此把守卫放进 `validate-upm.mjs`（CI 必跑）与 EditMode 测试，两处都会在目录/条目不一致时失败。
