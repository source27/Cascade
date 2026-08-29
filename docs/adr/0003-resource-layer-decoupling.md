# 资源层解耦：提供者无关契约 + 集成子包

`IResourceService` 契约清除 YooAsset 概念（`ResourcePlayMode` 移除、`ResourceInitOptions` 变为提供者无关基类，具体配置由组合根注入）；核心包零第三方资源系统依赖；`YooAssetResourceService` 迁至可选集成子包 `Integrations~/YooAsset/`（`com.source27.cascade.integrations.yooasset`，git `?path=` 安装）。启动流程本就经契约调用（LauncherFlow 无直接 `YooAssets.*`），仅需净化选项类型。

## 理由

Unity asmdef 无「可选包引用」——主包内独立 asmdef 会让不装 YooAsset 的消费方编译失败，集成子包是唯一正解；同时让 Addressables/自研提供者同构接入（留缝：实现契约方法即可）。示例工程用零依赖 `DemoResourceService` 演示。
