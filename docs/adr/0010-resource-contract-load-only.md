# 资源契约只含加载语义（延伸 ADR 0003）

**Status:** accepted — 最终成员列表见 [ADR 0015](0015-iresource-service-load-only-api.md)

`IResourceService` 限于 init/load/unload 等 **加载** 语义，**不含**版本检查/下载/更新。YooAsset、Addressables 的更新 API 留在各自 **集成包类型**（或 Starter 对那些类型的调用）上；Mobile Starter 编排资源热更，Indie 只初始化加载。

**理由：** 各后端更新模型不齐，揉进核心契约会强迫无更新游戏面对 no-op 或假方法；与「Cascade 不管热更」（ADR 0007）一致。换后端 = 改 Starter 更新段，私用可接受。
