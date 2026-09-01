# 非 Unity 注册表第三方 = 工程 peer，不进 package.json

**Status:** accepted

## 问题

`com.source27.cascade` 等包的 `package.json` 曾将 `com.cysharp.unitask@2.5.11`（以及 YooAsset / LitMotion / LoopScrollRect 等）写成 SemVer `dependencies`。

UPM 解析规则：

- `package.json` 的依赖值**只能**是 SemVer（禁止 git/file URL）。
- SemVer 依赖只从**已配置 registry**（默认 Unity registry + 工程 scopedRegistries）查找。
- UniTask 等不在 Unity 官方注册表。

结果：README 主推的

```
Add package from git URL → https://github.com/source27/Cascade.git?path=Cascade
```

在空工程上**必然**失败：`Package [com.cysharp.unitask@2.5.11] cannot be found`。  
只有工程已用 git/file/OpenUPM 提供同名包时才能过解析——与「一条 git URL 装上主包」的叙事冲突。

## 决策

1. 各 UPM `package.json` 的 `dependencies` **仅允许**：
   - `com.unity.*`（Unity 注册表可解析）
   - `com.source27.cascade*`（本仓库包族；子包声明对主包的 SemVer 依赖）
2. 其余第三方（UniTask、YooAsset、LitMotion、LoopScrollRect、…）为 **peer 依赖**：
   - 由消费工程 `Packages/manifest.json` 提供（git URL、`file:` 或 OpenUPM 等同名版本）
   - 版本 pin 与 git 提交写在 README / `docs/assembly.md` / Starter manifest
   - asmdef 继续引用对应程序集（缺 peer → 编译错误，而非 Package Manager 拒装）
3. `scripts/validate-upm.mjs` 强制：依赖必须 SemVer，且 name 匹配 `com.unity.*` 或 `com.source27.cascade*`。

## 不选的方案

| 方案 | 否决理由 |
|------|----------|
| package.json 写 UniTask git URL | UPM 非法，校验/安装均拒 |
| 主包内嵌/vendoring UniTask | 与已有 UniTask 工程双份程序集；版本升级重 | 
| 仅文档要求「先装 UniTask 再装 Cascade」、仍保留 SemVer 硬依赖 | 主推 git 安装路径对空工程永久红字；与实测错误一致 |
| 强制 OpenUPM scoped registry | 包无法注入工程 scopedRegistries；git-only 用户仍挂 |

## 后果

- 空工程可成功 `Add package from git URL` 装上 Cascade；未装 UniTask 时编译缺 `UniTask` 程序集。
- 集成包 / UiExtras 同理：YooAsset、LitMotion 等不进其 `package.json`。
- OpenUPM 若将来发布本包，第三方图不会自动拉齐，仍须工程或 registry 侧声明 peer（可另文补充 OpenUPM 安装模板）。
- 与 ADR 0008（主包不硬依赖 LitMotion/LoopScroll/HybridCLR）一致，并扩展到「凡非 Unity 注册表的第三方」。

## 参照

- ADR 0008 主包依赖最小化
- `Starters/Indie` / `Starters/Mobile` 的 manifest pin
