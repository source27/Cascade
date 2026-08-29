# 仓库布局与包结构

仓库根即 UPM 包 `com.source27.cascade`（`package.json` 在根，git URL 安装零配置），示例工程在 `Examples/CascadeExample/`（以 `file:../../` 引用），资源提供者以可选集成子包分发（`Integrations/YooAsset/`，`?path=` 安装），源码生成器源码放在 `Tools~/`（波浪号目录，Unity 不导入）而编译产物放在包内 `Roslyn/`（无 asmdef，按 Unity 官方规则作用于全工程程序集）。

## Considered Options

- 示例工程放独立仓库：排除——单仓库便于同步与联调。
- 生成器独立包：排除——单仓库多包 git 安装需 `?path=`，收益不抵复杂度。
- `Tools~/` vs client 的 Assets 外 `Tools/`：包根即仓库根，波浪号目录是 Unity 不导入的标准约定。
