# 项目扩展靠组合根；本轮不做 CQ 约束

不同项目的服务集与资源后端，通过 Starter/项目 **组合根** `override RegisterServices`（及资源工厂钩子）扩展：`base` + 增删替换；游戏专有服务只进 `ServiceRegistry`，经 `IGameHost.Services.Get<T>()` 取用，不把 Host 扩成服务目录。维持 ADR 0004：AOT 侧无 DI 容器。

Command/Query **本轮不**做代码约束或 mediator；现仅有 `IEventBus`。待有真实第二处命令路径再立法，避免空转抽象。

文档落地为三层：`docs/philosophy.md`、`docs/assembly.md`、`docs/coding.md`，加 CONTEXT/ADR。
