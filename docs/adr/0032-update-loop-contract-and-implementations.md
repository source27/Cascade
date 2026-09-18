# 更新循环：契约只面向订阅者，默认实现自持宿主

**Status:** accepted — 修订 [ADR 0022](0022-ui-and-localization-out-of-core.md) 中「`IUpdateLoop` 增加 `Tick*`（驱动者视角）」一条

## 决策

- `IUpdateLoop` 只留 `RegisterUpdate` / `RegisterLateUpdate` / `RegisterFixedUpdate`，各自返回 `IDisposable`。**反注册 = Dispose token**；不提供 `UnRegisterUpdate(Action)` —— `List.Remove(callback)` 只对方法组可靠，闭包/lambda 每次都是新实例会**静默失败**，而 token 精确、幂等、对任意委托都成立。
- 驱动面（`Tick*`）收回实现内部：`UpdateLoop` 成为纯 C# 内核（三张回调表 + `Tick*` + `Dispose`），由宿主驱动或测试直接驱动。
- 两个 Unity 实现，都满足同一契约：
  - **`UnityUpdateLoop`（默认）**：自己创建 `DontDestroyOnLoad` 宿主对象、用自身 MonoBehaviour 消息 tick、`Dispose` 时清空回调并销毁宿主（EditMode 用 `DestroyImmediate` 以便测试与工具确定性地看到对象消失）。
  - **`PlayerLoopUpdateLoop`**：注入 Unity `PlayerLoop`（插在 `Update.ScriptRunBehaviourUpdate` / `PreLateUpdate.ScriptRunBehaviourLateUpdate` / `FixedUpdate.ScriptRunBehaviourFixedUpdate` 之后），**零 GameObject**；`Dispose` 按 delegate 目标识别归属并移除（多实例互不影响）；锚点缺失时报错且保持不注入。
- 删除 `UnityUpdateDriver`：它存在的意义是"外部驱动任意 `IUpdateLoop`"，现在这份职责由实现自己承担。
- `BootstrapBase` 不含任何 loop/driver 字段：默认集合里 `if (!registry.TryGet<IUpdateLoop>(out _)) registry.Register<IUpdateLoop>(UnityUpdateLoop.Create(log));`，换驱动就是 `registry.Register/Replace<IUpdateLoop>(…)`。

## 理由

- 旧形状只兑现了一半：能换 loop **类型**，不能换**驱动方式**；项目注册自驱动实现会被 `UnityUpdateDriver` 双驱动，而 `BootstrapBase` 还留着两个只写一次的字段与一个指向已释放 loop 的引用。
- 与主包其它能力同构：`IResourceService`→`UnityResourcesService`、`ILogService`→`UnityLogService`、`ISaveService`→`PlayerPrefsSaveService`，循环没有理由是例外。
- 去掉 `Tick*` 后契约只表达订阅者需要的东西，"时间从哪来"纯属实现细节。

## Considered options

- 只做 `UnityUpdateLoop`（否：零 GameObject 是真实诉求，且两者共享同一内核，边际成本低）。
- 默认用 PlayerLoop 版（否：tick 时序由"与普通组件同序"变成"所有 Behaviour 之后"，对隐式依赖同帧顺序的代码是行为变化；注入还是运行期全局状态）。
- 契约上保留 `Tick*` 供外部 driver（否：那又要框架/宿主来当 driver，"谁驱动"重新变成框架替你决定）。
- 提供 `UnRegisterUpdate(Action)`（否：闭包不匹配会静默泄漏，见上）。

## 已知取舍

`PlayerLoopUpdateLoop` 依赖 Unity 的 PlayerLoop 锚点类型（`UnityEngine.PlayerLoop.*`），属内部约定；`UnityUpdateLoop` 会多一个隐藏的宿主 GameObject（名字 `[Cascade] UpdateLoop`，便于在 Hierarchy 里定位）。
