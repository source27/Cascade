# 单一组合根接缝：RegisterServices + 注册表 Replace/Remove

**Status:** accepted — 取代 [ADR 0025](0025-default-resource-provider-and-hooks.md) 的「每个默认一个独立钩子」与 [ADR 0023](0023-bootstrap-policy-lives-in-starter.md) 中的钩子清单

## 决策

1. `BootstrapBase` 的项目侧扩展面收敛为 **3 个虚方法**：`RegisterServices(registry)`（唯一服务注册接缝）、`CreateResourceInitOptions()`、`RunGameAsync(...)`。删除 `CreateLogService` / `CreateResourceService` / `CreateSaveService` / `CreateAudioService` / `CreateUpdateLoop` / `RegisterIfNotNull`。（**后续修订见 [ADR 0029](0029-resource-options-belong-to-provider-ctor.md)：`CreateResourceInitOptions` 亦删除，项目侧只剩 2 个虚方法。**）
2. **执行顺序**：`RegisterServices`（项目）→ 框架默认**按缺失补**（`TryGet` 为空才登记 log / resource / eventbus / audio / save）→ 解析/注册 `IUpdateLoop` → 资源 init → `RunGameAsync`。
3. `IServiceRegistry` 增加 `Replace<T>(instance)` 与 `Remove<T>()`：
   - `Replace`：契约缺失时等价 `Register`；同一实例 → no-op；否则**立即 Dispose 旧实例**（当它实现 `IDisposable`）并**占用旧槽位**（注册序驱动逆序释放，换实现不得改变别人的拆解次序）；`null` → `ArgumentNullException`；槽位查找用 `ReferenceEquals`（`UnityEngine.Object.Equals` 比较的是对象身份）。
   - `Remove`：Dispose 旧实例 + 摘除释放链，返回是否存在过。
   - `Register` 保持「同契约重复即抛」，继续抓真·重复注册。
4. 两者都定位为**组合根阶段**操作；运行期换实现不由注册表接管（需要时由服务自己支持重绑）。

## 理由

- **旧状是两个机制做同一件事**：既 override `CreateX` 又 override `RegisterServices`；同一接缝还有两套优先级（`IUpdateLoop` 既能被钩子创建、又能被注册表预注册且后者胜）；替换默认实现只能走 `CreateX`（重复 `Register` 抛异常），"不要某默认"只能让钩子返回 null —— 同一诉求三种表达法。
- **基类会变成服务目录**：5 个 `CreateX` 是默认清单的镜像，每加一个默认就多一个虚方法——与 ADR 0011 否掉「把 `IGameHost` 扩成服务目录」同因。
- **"项目先、默认补缺"是为了规避捕获陷阱**：若改成「默认先注册、项目 `base` 后 `Replace`」，默认实现里捕获了其它默认的会指向旧实例——`AudioService(resources, log)` 会一直攥着 `UnityResourcesService`，项目换成 YooAsset 后音频仍走 `Resources.Load`。项目先注册、默认随后从注册表取依赖，接线天生正确。
- **`Replace` 必须负责 Dispose**：否则换实现＝静默泄漏（旧 `LocalizationService` 的 `SemaphoreSlim`、旧 `AudioService` 的句柄与订阅都不会释放），且旧实例会一直活到注册表销毁。

## Considered options

- 保留每默认钩子：否——两套机制 + 基类服务目录。
- 只加 `RegisterServices` 但维持「默认先、子类 `base` 后 `Replace`」：否——捕获陷阱（事件总线/音频会绑旧依赖）。
- 默认注册也交给子类：否——主包就不再"提供默认实现"。
- 引入 DI 容器/工厂表：否——ADR 0004 禁止 AOT 侧容器，5 个服务也不值当。

## 已知取舍

`Remove<T>()` 无法在 `RegisterServices` 里表达「不要某个默认」（默认是**之后**才补的）。需要切除时在 `RunGameAsync` 早期或模块安装处 `Remove<T>()`；这是为顺序正确性付的代价。
