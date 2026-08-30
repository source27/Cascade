# Indie Starter 最低完成定义

**Status:** accepted — resolves [决策：Indie Starter 最低完成定义](https://github.com/source27/Cascade/issues/25)

## 工程

- 路径：**新建** `Starters/Indie`（不从 Mobile/Example 复制再删）。
- Unity **2022.3**；`com.unity.addressables` 与 [ADR 0017](0017-addressables-integration-surface.md) **同一精确 pin**。
- 命名空间 **`Cascade.Indie`**；组合根 **`IndieBootstrapEntry`**；程序集 **`Cascade.Indie`**（全 AOT）。主逻辑同进程直接调用（如 `GameEntry.Start(IGameHost, CT)`），无热更反射程序集。
- 与 Mobile：**松对称**（Bootstrap 场景、Scripts、README、`Cascade.*` 前缀）；不建空 HotUpdate 目录。

## DoD（最低完成）

1. 进 Play：薄 Bootstrap 流水线跑通 → `RunGameAsync` → 主逻辑  
2. Addressables 至少 **成功 load 1 个** address  
3. 默认本地化 init 成功（catalog + 表经 Addressables `LoadRawBytes` / TextAsset 约定）  
4. **最小 UI 一页**冒烟（非可玩）

## manifest

必有：`com.source27.cascade`、`com.source27.cascade.integrations.addressables`、pin 定的 Addressables、UniTask、ugui、TMP。  
**默认不带** `cascade.modules.uiextras`、YooAsset、HybridCLR。

## 非目标

HybridCLR / 热更 DLL / AOTGenericReferences；YooAsset / PatchWindow / 资源更新编排；Mobile 构建窗 / DevCDN；可玩切片；红点与多设备输入等能力包（按需另加）。
