# 可选能力 = 独立 UPM 子包

红点、多设备输入、`cascade.ui.extras` 等按需能力，以 **独立 UPM 子包** 分发（与 `Integrations/YooAsset` 同构），不塞进主包「永不删除的 Module 程序集」充数。依赖方向：能力/集成包 → Cascade，永不反向。项目通过 manifest 选择安装；仅不 `Register` 不足以称为可选（仍占依赖与误用面）。

空壳 `Cascade.Module` 不再作为可选能力的归宿。

**Considered options：** 源码进主包只靠不注册（假可选）；只放某个 Starter（跨 Mobile/Indie 的 UI 能力会复制）。
