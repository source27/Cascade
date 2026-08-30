# 无 DI 容器：显式注册 + 手写构造注入

**Status:** accepted — 项目扩展方式见 [ADR 0011](0011-composition-root-extension-and-no-cq.md)

维持 `ServiceRegistry` 显式实例注册（`Register<T>(instance)` / `Get<T>()` / 逆序 Dispose）与手写构造注入（如 `EventBus(ILogService log = null)`），不引入任何 DI 容器。

## 理由

框架是 AOT 固化层（HybridCLR 热更架构的稳定基座）——容器普遍依赖反射/IL 发射/代码生成，放固化层会膨胀 AOT 元数据，放热更层则违反依赖方向（热更→AOT，禁止反向）；公共框架零第三方依赖原则；7 服务浅图自动装配收益≈0。消费方若需 DI，可在自己的热更层自行引入（`IGameHost.Services` 桥不挡）。
