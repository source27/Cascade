# 结构改造地图结束与 to-spec 交接

**Status:** accepted — resolves [决策：地图结束条件与 to-spec 交接](https://github.com/source27/Cascade/issues/27); closes map [Cascade 结构改造](https://github.com/source27/Cascade/issues/17)

## 地图关闭准则

1. 地图全部子票 closed  
2. Decisions so far 覆盖 ADR 0006–0018 与 `docs/research/*` 三份笔记  
3. Destination（包边界 / Bootstrap 切割 / load-only API / 双 Starter）均已决策  
4. **不**要求代码或 philosophy/assembly/coding 已完成  

关地图 = **路径清晰**，可 `/to-spec`，不是改造完工。

## to-spec 必读输入

- `CONTEXT.md`
- ADR **0006–0019**（及仍有效的 0001–0004 交叉引用）
- `docs/research/launcher-bootstrap-cut-surface.md`
- `docs/research/iresource-service-api-inventory.md`
- `docs/research/addressables-iresource-mapping.md`
- 地图 #17 Decisions so far（索引）

## 规格建议章节

1. 目标与非目标  
2. 包/程序集清单（0013）  
3. 主包改造（Bootstrap、load-only、依赖、Module、改名、ui.extras）  
4. Yoo 集成调整（0015）  
5. Addressables 集成（0017）  
6. Mobile 迁移（0016）  
7. Indie 新建与 DoD（0018）  
8. 实现波次（下表）  
9. 验收冒烟  
10. 文档三件套为 **P4 交付物**（本 ADR 不写细纲）

## 实现波次（写死）

| 波次 | 内容 |
|------|------|
| **P0** | 主包：load-only cleave、Bootstrap 瘦身+`Cascade.Bootstrap` 改名、删 Module、去 HybridCLR/LitMotion/LoopScroll、抽 `Modules/UiExtras`、测试变瘦 |
| **P1** | Yoo 集成：更新 API 仅具体类型、DTO 改名；主包测试绿 |
| **P2** | `git mv` → `Starters/Mobile`；`Cascade.Mobile.*`；`RunGameAsync` 接线；Mobile 冒烟 |
| **P3** | Addressables 集成 + 新建 `Starters/Indie`；Indie DoD 冒烟 |
| **P4** | `docs/philosophy.md` / `assembly.md` / `coding.md` + README 收尾 |

约束：主包可编译 **先于** Mobile 接新 API（与 ADR 0016 一致）。

## 雾的处理

- 默认服务白名单 / GameHost 可见性：P0 实现时顺手，不另开图  
- validate-upm/CI：to-spec 可选章  
- 文档细纲：P4  
- 红点 / 多设备输入等能力包：**另图**，本图 Out of scope  
