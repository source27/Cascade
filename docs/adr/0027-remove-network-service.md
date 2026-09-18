# 删除 INetworkService：网络不是核心默认

**Status:** accepted — 修订 [ADR 0025](0025-default-resource-provider-and-hooks.md) 的默认服务集（网络一项删除）

## 决策

- 删除 `Cascade.Service` 的 `INetworkService`（含 `NetworkConnectionState`）与 `NullNetworkService`（整个 `Stubs/` 目录随之消失）。
- `BootstrapBase` 删除 `CreateNetworkService` 钩子与注册：核心默认服务集收敛为 **日志 / 资源 / 存档 / 音频 + 更新循环**。（音频随后也出包，见 [ADR 0030](0030-audio-module-and-dependency-cleanup.md)：现为 日志 / 资源 / 存档 / 事件总线 + 更新循环。）
- 需要网络的工程在自己程序集里定义并注册自己的网络服务（`registry.Register<IMyNetwork>(…)`），核心包不再提供空契约与空实现。

## 理由

- 网络不是必需品：单机、纯离线、桌面小工具类项目都不该在服务表里看到一个永远 `Disconnected` 的假服务。
- 那份契约本身也没有可用语义：只有 `Connect/Disconnect/Send(byte[])`，没有接收侧、没有事件、没有超时/重连；它的默认实现 `NullNetworkService.Connect` 还会把状态翻成 `Connected` —— 一个会撒谎的桩。
- 框架无法在「HTTP / WebSocket / UDP / 长连接房间」之间抽象出有意义的公共契约，硬留一个就是 ADR 0011「不为尚未存在的用例先上抽象」的反例；真需要时按项目形态写，或做成可选 Module/Integration。

## Considered options

- **保留契约但默认不注册**（否：主包仍背一份无实现承诺的公共 API，且它已无消费者）。
- **搬进可选模块**（否：没有可产出的实现，模块里只会是同一个空壳）。
- **补全成真正的网络能力**（否：超出「核心只保留最基础」的取向，且需要先有真实项目形态）。
