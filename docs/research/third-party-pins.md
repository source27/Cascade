# 研究：第三方包来源与 git 版本固化（Ticket #2）

> 目标：确定客户端用到的 5 个第三方 UPM 包的来源（上游 git 仓库 + 可固化版本），检查客户端副本是否被本地改动，并给出框架提取仓库 `package.json` 的依赖草案。
> 研究方式：以主源为准 —— 读取客户端 `Packages/manifest.json`、`Packages/<pkg>/package.json` 与 asmdef；用 `git ls-remote` 校验上游 tag；浅克隆上游仓库到匹配 tag，与客户端副本做 `diff -rq --strip-trailing-cr` 对比。

## 结论速览

| 包名 | client 版本 | file: 指向 / 是否被改 | 上游 git URL | 锁定 ref | license | 备注 |
|---|---|---|---|---|---|---|
| com.cysharp.unitask | 2.5.11 | `Packages/com.cysharp.unitask@2.5.11/`，被 client 仓库跟踪（非独立 git 仓库）；**本地补丁**：新增 `External/YooAsset/` 集成（`UniTask.YooAsset.asmdef` + 2 个扩展类），`Runtime/_InternalVisibleTo.cs` 增加 `InternalsVisibleTo("UniTask.YooAsset")`；package.json 仅格式化差异 + 追加 `repository.revision` | https://github.com/Cysharp/UniTask.git | tag `2.5.11` → `2e993ff18f28c931602a07292df0b0804eebef99`（与 client package.json 记录的 revision 完全一致） | MIT | UPM 包位于上游 `src/UniTask/Assets/Plugins/UniTask/`；上游该目录**无** `External/YooAsset`，此集成是客户端自研，纯 git 依赖不会带入 |
| com.tuyoogame.yooasset | 3.0.5 | **本地补丁**：Editor 菜单路径 `YooAsset/…` → `ThirdParty/YooAsset/…`（BundleBuilderWindow、BundleCollectorWindow/Setting、BundleDebuggerWindow、BundleReporterWindow、HomePage、`Runtime/Settings/YooAssetSettings.cs` 的 CreateAssetMenu）；删除 `Samples~/`；package.json 格式化 + 追加 revision | https://github.com/tuyoogame/YooAsset.git | tag `3.0.5` → `94422fc41491228eed0999ce4845d7b23ee2b8ae`（与 package.json 记录的 revision 一致） | Apache-2.0 | UPM 包位于上游 `Assets/YooAsset/`；依赖 `com.unity.scriptablebuildpipeline 1.21.25` |
| com.code-philosophy.hybridclr | 8.13.0 | **本地补丁**：`Editor/Commands/*.cs`（7 个命令）与 `Editor/Settings/MenuProvider.cs` 菜单路径 `HybridCLR/…` → `ThirdParty/HybridCLR/…` | https://github.com/focus-creative-games/hybridclr_unity.git | tag `v8.13.0` → `ca7f87b6a72f3739f99a5ad0c957c7aae0cbd922` | MIT | **包名/仓库名不一致**：包名 `com.code-philosophy.hybridclr` 对应的 UPM 仓库是 `hybridclr_unity`（仓库根目录即 UPM 包，package.json 在根）；核心运行时是另一个仓库 `focus-creative-games/hybridclr`（tag 亦为 v8.x 系列） |
| com.annulusgames.lit-motion | 2.0.2 | 代码**零改动**；`.meta` 文件被 Unity 导入时补全（GUID 与上游一致）；包内无 LICENSE 文件 | https://github.com/AnnulusGames/LitMotion.git | tag `v2.0.2` → `0b4c588ee75a07198841d92aab653e6b39445089` | MIT（上游仓库根 LICENSE） | UPM 包位于上游 `src/LitMotion/Assets/LitMotion/` |
| com.annulusgames.lit-motion.animation | 2.0.2 | **本地补丁**：`Runtime/LitMotionAnimation.cs` 两处 `Debug.LogException(ex)` → `Debug.LogException(ex, context: this)`；`.meta` 补全同上 | 同上（同一仓库） | 同上 tag | MIT | UPM 包位于上游 `src/LitMotion/Assets/LitMotion.Animation/`；依赖 lit-motion `2.0.0` |
| me.qiankanglai.loopscrollrect | 1.1.5 | 代码**零改动**；仅删除上游 `Images~/`、`Samples~/` | https://github.com/qiankanglai/LoopScrollRect.git | tag `v1.1.5` → `a74a705e1c9d0f73ea1a441dabb23b22f3283071` | MIT | 仓库根目录即 UPM 包 |

## 1. `file:` 指向解析与仓库状态

客户端 `Packages/manifest.json` 中 5 个第三方包全部以 `file:` 相对路径引用（如 `"com.cysharp.unitask": "file:com.cysharp.unitask@2.5.11"`），目标都在 `E:\Projects\client\Packages\` 下（[manifest.json](../../../client/Packages/manifest.json)）。

`git -C <每个包目录> rev-parse --git-dir` 均解析到 `E:/Projects/client/.git`、remote 为内网 `http://182.92.2.103:3000/f13/client.git` —— 即这些包**不是独立 git 仓库**，而是被 client 仓库直接跟踪的 vendor 副本（"内网 CDN 测试"提交 `098060b` 引入，见 `git status --short` 干净、`git log --oneline -1`）。因此"是否偏离上游"只能通过与上游 tag 对比判断。

对比方法：`git clone --depth 1 --branch <tag>` 各上游仓库到临时目录，与客户端副本执行 `diff -rq --strip-trailing-cr`（消除 CRLF 干扰；客户端副本普遍 CRLF、上游 LF）。

### 各包差异明细（相对上游 tag）

- **UniTask 2.5.11**：`package.json` 仅格式差异（上游单行压缩 JSON vs 客户端展开）+ 客户端追加 `repository.revision: 2e993ff…` 与 `publishConfig`；`Runtime/_InternalVisibleTo.cs` 多一行 `InternalsVisibleTo("UniTask.YooAsset")`；新增 `External/YooAsset/`（`UniTask.YooAsset.asmdef`、`AsyncOperationBaseExtensions.cs`、`HandleBaseExtensions.cs`）。上游 UPM 目录（`src/UniTask/Assets/Plugins/UniTask/`）只有 Editor/Runtime/package.json，**无任何 External/YooAsset 内容**（`find UniTask -iname "*YooAsset*"` 为空）—— 该集成是客户端自研代码。
- **YooAsset 3.0.5**：CR 归一后仅 9 个文件真实差异 + `Samples~/` 仅上游有：`Editor/Assembly/AssemblyInfo.cs` 等大批文件仅行尾差异；真实差异集中在 `Editor/BundleBuilder/BundleBuilderWindow.cs`、`Editor/BundleCollector/{BundleCollectorSetting,BundleCollectorWindow}.cs`、`Editor/BundleDebugger/BundleDebuggerWindow.cs`、`Editor/BundleReporter/BundleReporterWindow.cs`、`Editor/HomePage.cs`、`Runtime/Settings/YooAssetSettings.cs`，内容为菜单项 `YooAsset/…` → `ThirdParty/YooAsset/…`（含 `[CreateAssetMenu]`），另 `package.json` 格式化 + 追加 revision。
- **HybridCLR 8.13.0**：`Editor/Commands/` 下 7 个命令文件 + `Editor/Settings/MenuProvider.cs`，全部是 `[MenuItem("HybridCLR/…")]` → `[MenuItem("ThirdParty/HybridCLR/…")]` 的菜单改名。
- **LitMotion 2.0.2 / 2.0.2.animation**：代码无改动（animation 仅 2 处 `Debug.LogException` 增加 `context: this` 参数）；大量 `.meta` 差异为上游 meta 只有 `guid:` 一行、客户端是 Unity 导入后补全的完整 meta（GUID 相同，如 `Box.cs.meta` guid `858a24f7a0fbb4edcb5727a3656af8a3` 两侧一致）。
- **LoopScrollRect 1.1.5**：除上游 `Images~/`、`Samples~/` 外完全一致。

> 小结：本地改动都是「菜单归类到 ThirdParty/ + 删 Samples + package.json 注解 revision」这类工程性调整，**没有业务逻辑级改动**；唯一需要特别保留的是 UniTask 的 `External/YooAsset` 自研集成。

## 2. 上游 tag 校验（锁定 ref）

| 包 | 上游 tag | 检出 commit | 与 client package.json revision 一致性 |
|---|---|---|---|
| UniTask | `2.5.11` | `2e993ff18f28c931602a07292df0b0804eebef99` | 完全一致（`repository.revision`） |
| YooAsset | `3.0.5` | `94422fc41491228eed0999ce4845d7b23ee2b8ae` | 完全一致（`repository.revision`） |
| HybridCLR (hybridclr_unity) | `v8.13.0` | `ca7f87b6a72f3739f99a5ad0c957c7aae0cbd922` | 包内无 revision 字段；tag 校验 `git ls-remote refs/tags/v8.13.0` |
| LitMotion | `v2.0.2` | `0b4c588ee75a07198841d92aab653e6b39445089` | 包内无 revision 字段；tag 校验 `git ls-remote refs/tags/v2.0.2` |
| LoopScrollRect | `v1.1.5` | `a74a705e1c9d0f73ea1a441dabb23b22f3283071` | 包内无 revision 字段；tag 校验 `git ls-remote refs/tags/v1.1.5` |

校验命令：`git ls-remote https://github.com/<repo>.git refs/tags/<tag> refs/tags/<tag>^{}`。注意 HybridCLR / LitMotion / LoopScrollRect 的 tag 名带 `v` 前缀（`v8.13.0`、`v2.0.2`、`v1.1.5`），不带 `v` 直接查会查不到。

## 3. License 与 UPM git 依赖合规性

| 包 | 客户端/上游 LICENSE 文件 | License | 允许 UPM git 依赖 |
|---|---|---|---|
| UniTask | 客户端包内无 LICENSE，上游仓库根 `LICENSE` = MIT（Copyright (c) 2019 Yoshifumi Kawai / Cysharp, Inc.）；package.json `"license": "MIT"` | MIT | 是 |
| YooAsset | 客户端 `LICENSE.md` = Apache License 2.0 | Apache-2.0 | 是 |
| HybridCLR | 客户端 `LICENSE` = MIT（Copyright (c) 2025 Code Philosophy Technology Ltd.） | MIT | 是 |
| LitMotion | 客户端包内无 LICENSE，上游仓库根 `LICENSE` = MIT（Copyright (c) 2023 Yusuke Nakada） | MIT | 是 |
| LoopScrollRect | 客户端 `LICENSE.md` = MIT（Copyright (c) 2017 Kanglai Qian） | MIT | 是 |

以上全部为宽松许可（MIT / Apache-2.0），允许以 git URL 方式被引用与再分发；Unity UPM 官方支持 git 依赖（含子目录 `?path=` 与 `#ref` 固化），不涉及许可证冲突。

## 4. 框架 asmdef 依赖（内置包 + 第三方确认）

框架 4 个 asmdef（`E:\Projects\client\Assets\Scripts\Framework\{Launcher,Service,System,Module}\*.AOT.asmdef`）的 `references` 汇总：

| asmdef | references（去重） |
|---|---|
| Launcher.AOT | Service.AOT, System.AOT, Module.AOT, **YooAsset**, **HybridCLR.Runtime**, **UniTask**, **UnityEngine.UI** |
| Service.AOT | **YooAsset**, **UniTask** |
| System.AOT | Service.AOT, **UniTask**, **UnityEngine.UI**, **Unity.TextMeshPro**, **LoopScrollRect.Runtime**, **LitMotion**, **LitMotion.Extensions** |
| Module.AOT | System.AOT |

- 第三方引用恰好对应 vendored 包的 asmdef：`YooAsset`（com.tuyoogame.yooasset → `Runtime/YooAsset.asmdef`）、`HybridCLR.Runtime`（com.code-philosophy.hybridclr → `Runtime/HybridCLR.Runtime.asmdef`）、`UniTask`（com.cysharp.unitask → `Runtime/UniTask.asmdef`）、`LoopScrollRect.Runtime`（me.qiankanglai.loopscrollrect → `Runtime/LoopScrollRect.Runtime.asmdef`）、`LitMotion` + `LitMotion.Extensions`（com.annulusgames.lit-motion → `Runtime/LitMotion.asmdef` + `Runtime/Extensions/LitMotion.Extensions.asmdef`）。
- 内置包仅两个：`UnityEngine.UI` = `com.unity.ugui`（manifest 版本 `1.0.0`）、`Unity.TextMeshPro` = `com.unity.textmeshpro`（manifest 版本 `3.0.9`）。
- **框架 asmdef 未引用任何其他第三方包**（全部引用条目已在上面穷举）。

## 5. cascade `package.json` 依赖草案

框架提取仓库建议直接用 **git URL + commit SHA** 固化（比 tag 更硬；tag 名见第 2 节可互换）：

```json
{
  "dependencies": {
    "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=/src/UniTask/Assets/Plugins/UniTask#2e993ff18f28c931602a07292df0b0804eebef99",
    "com.tuyoogame.yooasset": "https://github.com/tuyoogame/YooAsset.git?path=/Assets/YooAsset#94422fc41491228eed0999ce4845d7b23ee2b8ae",
    "com.code-philosophy.hybridclr": "https://github.com/focus-creative-games/hybridclr_unity.git#ca7f87b6a72f3739f99a5ad0c957c7aae0cbd922",
    "com.annulusgames.lit-motion": "https://github.com/AnnulusGames/LitMotion.git?path=/src/LitMotion/Assets/LitMotion#0b4c588ee75a07198841d92aab653e6b39445089",
    "com.annulusgames.lit-motion.animation": "https://github.com/AnnulusGames/LitMotion.git?path=/src/LitMotion/Assets/LitMotion.Animation#0b4c588ee75a07198841d92aab653e6b39445089",
    "me.qiankanglai.loopscrollrect": "https://github.com/qiankanglai/LoopScrollRect.git#a74a705e1c9d0f73ea1a441dabb23b22f3283071",
    "com.unity.ugui": "1.0.0",
    "com.unity.textmeshpro": "3.0.9"
  }
}
```

要点：
- HybridCLR 的 `?path=` 不需要 —— `hybridclr_unity` 仓库根就是 UPM 包。
- LitMotion 两个包同仓库、同 ref，用 `?path=` 区分子目录。
- 纯 git 依赖**不会带入客户端本地补丁**：UniTask 的 `External/YooAsset` 集成（自研）、全部 `ThirdParty/…` 菜单改名、YooAsset/HybridCLR 的 `Samples~` 删除都不在 git 依赖里，需另行处理（保留补丁文件或接受上游原版菜单）。

## 来源

- 客户端 manifest：[E:\Projects\client\Packages\manifest.json](E:/Projects/client/Packages/manifest.json)（file: 引用、内置包版本、scopedRegistries openupm）
- 各 vendored 包 package.json / asmdef / LICENSE：`E:\Projects\client\Packages\<pkg>\`（包名、版本、repository.revision、asmdef 名、LICENSE 文本）
- 框架 asmdef：`E:\Projects\client\Assets\Scripts\Framework\{Launcher,Service,System,Module}\*.AOT.asmdef`
- 上游 tag 校验：`git ls-remote https://github.com/{Cysharp/UniTask,tuyoogame/YooAsset,focus-creative-games/hybridclr_unity,AnnulusGames/LitMotion,qiankanglai/LoopScrollRect}.git refs/tags/<tag>`
- 上游对比：`git clone --depth 1 --branch <tag>` 后 `diff -rq --strip-trailing-cr`（临时目录，未入库）
- UPM git 依赖语法：Unity Manual "Git dependencies"（`?path=` 子目录、`#ref` 固化）
