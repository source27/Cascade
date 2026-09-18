# 资源初始化参数收进 provider 构造函数；Bootstrap 只剩两个虚方法

**Status:** accepted — 修订 [ADR 0015](0015-iresource-service-load-only-api.md) 的 init 签名与 [ADR 0028](0028-single-composition-seam.md) 的虚方法计数

## 决策

- `IResourceService.InitializeAsync(ResourceInitOptions options, CancellationToken)` → **`InitializeAsync(CancellationToken cancellationToken = default)`**。
- 删除空基类 `ResourceInitOptions`；provider 各自的 options 类不再继承它（`YooAssetResourceInitOptions`、`AddressablesResourceInitOptions`），改为**构造参数**：
  - `new YooAssetResourceService(YooAssetResourceInitOptions options)`（options 必填，`null` 抛 `ArgumentNullException`——原来那个「requires YooAssetResourceInitOptions」的运行时异常消失，因为不可能构造出来）。
  - `new AddressablesResourceService(AddressablesResourceInitOptions options = null)`。
  - `UnityResourcesService` 无 options。
- `BootstrapBase` 删除 `CreateResourceInitOptions` 虚方法：项目侧只剩 **`RegisterServices`** 与 **`RunGameAsync`** 两个虚方法；流水线调用 `resources.InitializeAsync(cancellationToken)`。
- Starter 侧：Mobile 的 playMode → Yoo options 变成私有 `CreateYooOptions()`，`YooAssetResourceService` 与 `MobileBootstrapConfiguration` 共用**同一个 options 实例**（过去会被构造两次）；`MobileBootstrapConfiguration.ResourceInitOptions` 属性删除。

## 理由

options 是**provider 的构造数据**，不是流水线数据——它描述 package 名、播放模式、CDN 根、init handle 行为，这些知识就在构造 provider 的那一行手里。原来的钩子只是把这份数据从 provider 构造函数搬到了基类上，再让流水线转发一次，于是：基类多一个虚方法、契约多一个参数与一个只为当参数而存在的空基类、Mobile 里 options 被构造两次。顺带也与 `ILocalizationService.InitializeAsync(ct)` 的形状统一了。

## Considered options

- 保留 `CreateResourceInitOptions`（否：成本虽低，但它让"参数在谁手里"这件事变模糊，且空基类只有它一个消费者）。
- 让注册表承载 options（`Register<ResourceInitOptions>(…)`）：否——配置进服务表，语义摩擦，流水线仍要 `TryGet`。
- 保留 `ResourceInitOptions` 空基类以防将来有统一消费方：否——当下零消费方，属 ADR 0011 反对的"为未存在的用例留抽象"；需要时由 provider 自己的 options 类型承担。
