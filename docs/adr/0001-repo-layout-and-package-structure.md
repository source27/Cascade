# 仓库布局与包结构

仓库根即 UPM 包 `com.source27.cascade`（`package.json` 在根，git URL 安装零配置），示例工程在 `Examples~/CascadeExample/`（manifest 以 `file:../../../` 相对路径引用，注意 Unity 的 `file:` 按 manifest 所在目录解析），资源提供者以可选集成子包分发（`Integrations~/YooAsset/`，`?path=` 安装），源码生成器源码放在 `Tools~/`（波浪号目录，Unity 不导入）而编译产物放在包内 `Roslyn/`（无 asmdef，按 Unity 官方规则作用于全工程程序集）。

**`~` 后缀是硬性要求**（`Samples~`/`Documentation~` 同款官方约定）：包内的非包内容目录必须带 `~`，否则 Unity 会把它们当包资产导入。实测教训：示例工程（嵌套 Unity 工程）与集成子包最初放在不带 `~` 的 `Examples/`、`Integrations/` 下，结果示例工程的 `Assets/` 被同时当作工程资产与包资产导入 → GUID 冲突 → Unity 改写 meta → 文件变化触发再导入 → 导入无限循环（迭代数永涨）；集成子包的 asmdef 还会因引用未安装的 YooAsset 使主包安装即报编译错。

## Considered Options

- 示例工程放独立仓库：排除——单仓库便于同步与联调。
- 生成器独立包：排除——单仓库多包 git 安装需 `?path=`，收益不抵复杂度。
- `Tools~/` vs client 的 Assets 外 `Tools/`：包根即仓库根，波浪号目录是 Unity 不导入的标准约定。
