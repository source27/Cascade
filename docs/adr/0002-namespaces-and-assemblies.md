# 命名空间与程序集布局

四层程序集去 `.AOT` 后缀、单数、程序集名 = 命名空间名：`Cascade.Launcher` / `Cascade.Service` / `Cascade.Core` / `Cascade.Module`（空壳占位），子命名空间机械平铺；编辑器工具为 `Cascade.Editor`，测试为 `Cascade.Tests`；`IGameLogicHost` 改名为 `IGameHost`，UI 前缀统一大写（`UICoordUtility`/`WorldUIHost` 等）。

**Core 命名来源**：client 的 `DB.System` 与 .NET `System` 冲突——实测（dotnet 10）`namespace X.System` 内裸 `using System;` 必编译失败（`System` 绑定到外层 X 的成员命名空间，CS0234/CS0246），client 的平铺文件因此处于编译坏态、Animation 文件靠 `global::` 存活。System 层改名 **Core** 根治冲突，提取时原样复制即修复。
