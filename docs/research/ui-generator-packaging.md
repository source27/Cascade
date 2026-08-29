# 研究：UI 源码生成器的 UPM 分发机制

- 研究范围：client 工程中 `DB.UI.SourceGenerator`（Roslyn 源码生成器，位于 `Assets` 之外）如何进入 Unity 编译；Unity 2022.3 对 UPM 包内分发 Roslyn 分析器/源码生成器的官方约定；cascade 的具体打包布局建议。
- 结论摘要：
  1. client 的机制是 **编译产物 DLL + `RoslynAnalyzer` 资产标签**：生成器源码工程留在 `Tools/`（Assets 之外），编译出的 `DB.UI.SourceGenerator.dll` 提交在 `Assets/Scripts/GameLogic/Analyzers/` 下，其 `.meta` 带 `RoslynAnalyzer` 标签与 Editor-only 平台设置。Unity 识别该标签后把 DLL 当作分析器/源码生成器注入脚本编译。**没有任何 asmdef 引用、csc.rsp 或 Editor 脚本参与接线**。
  2. Unity 2022.3 官方手册（`roslyn-analyzers.html`）明确规定：生成器必须编译为 .NET Standard 2.0 目标、引用 `Microsoft.CodeAnalysis` **3.8**；把 DLL 放入工程（Assets 或包）后，在 Plugin Inspector 关闭 Any Platform、关闭 Editor/Standalone，并打上大小写敏感、必须精确的 **`RoslynAnalyzer`** 标签；分析器作用域由其所在目录与 asmdef 的相对位置决定。
  3. cascade 建议：生成器源码独立成包外 dotnet 工程（镜像 client 的 `Tools/` 先例），编译产物提交到包内 `Roslyn/` 目录（该目录及包根不放 asmdef → 按官方作用域规则作用于全工程程序集），生成器按元数据名自门控；生成注册表经 `RegisterAll` 在组合根消费，`Bindings.g.cs` 由 Editor 菜单工具写出并随消费方代码提交，运行期零反射。

---

## 1. client 现状：生成器如何进入 Unity 编译

### 1.1 生成器源码工程（Assets 之外）

源码与工程文件：

- `Tools/DB.UI.SourceGenerator/DB.UI.SourceGenerator.csproj`：`TargetFramework=netstandard2.0`、`LangVersion 8.0`、`AssemblyName=DB.UI.SourceGenerator`，唯一的包引用为 `Microsoft.CodeAnalysis.CSharp` **3.8.0**（`PrivateAssets="all"`），并 `NoWarn RS2008`。
- `Tools/DB.UI.SourceGenerator/UISourceGenerator.cs`：经典的 `[Generator] public sealed class UISourceGenerator : ISourceGenerator`。`Execute` 中按**元数据名**查找 `DB.System.UIAttribute` / `DB.System.UIBase` / `DB.System.IUIArgs\`1`（`GetTypeByMetadataName`），找不到即直接返回（自门控）；仅遍历**当前被编译程序集**（`compilation.Assembly.GlobalNamespace`）中 `F13.GameLogic.UI` 命名空间下带 `[UI]` 的类型，产出 `F13.GameLogic.UI.Generated.UIRegistryGenerated`（含 `RegisterAll` 与每个页面的私有 Factory），并报告 DBUI001–DBUI007 诊断。

关键点：**源码在 Assets 之外、不参与 Unity 导入**；`*.csproj` 全局被 `.gitignore` 忽略，仅 `!/Tools/DB.UI.SourceGenerator/*.csproj` 与 `!/Tools/DB.UI.SourceGenerator.Tests/*.csproj` 放行（见 `E:\Projects\client\.gitignore:44-46`），即生成器**源码**入库、其 `bin/obj` 产物不入库。

### 1.2 编译产物 DLL 与 RoslynAnalyzer 标签（核心机制）

编译后的 DLL 提交在工程内：

- `Assets/Scripts/GameLogic/Analyzers/DB.UI.SourceGenerator.dll`（24.5KB，git 已跟踪，最近一次提交 `92d402b`）。
- 其 `.meta`（`Assets/Scripts/GameLogic/Analyzers/DB.UI.SourceGenerator.dll.meta`）：

```yaml
labels:
- RoslynAnalyzer
PluginImporter:
  platformData:
  - first: { Any: }        second: { enabled: 0 }
  - first: { Editor: Editor }  second: { enabled: 1 }
```

这就是进入 Unity 编译的全部机制：**`RoslynAnalyzer` 资产标签**让 Unity 把该插件 DLL 识别为 Roslyn 分析器/源码生成器并注入脚本编译；平台开关（Any 关闭、Editor 开启）只影响其作为运行期插件的导入/打包（分析器永远不进入 Player）。工程内**不存在** csc.rsp、csproj 生成钩子或动态加载该 DLL 的 Editor 代码（全仓库 grep `DB.UI.SourceGenerator|UISourceGenerator` 与 `RoslynAnalyzer|Analyzers` 仅命中源码、meta 与文档）。

### 1.3 asmdef 与作用域

- `Assets/Scripts/GameLogic/GameLogic.HotUpdate.asmdef` 与 `Assets/Scripts/Framework/System/System.AOT.asmdef` 的 JSON 中**没有任何分析器引用字段**（无 RoslynAnalyzer 引用、无 overrideReferences 指向该 DLL）——接线完全靠 1.2 的标签。
- 按官方作用域规则（见 §2.2），DLL 位于 `Assets/Scripts/GameLogic/Analyzers/`——父目录 `Assets/Scripts/GameLogic/` 内含 `GameLogic.HotUpdate.asmdef`，故该分析器**仅作用于 GameLogic.HotUpdate 程序集及其引用者**（引用者：`Assets/Scripts/Editor/DB.Foundation.Editor.asmdef`、`Assets/Scripts/Tests/DB.Foundation.Editor.Tests.asmdef`、`Assets/Scripts/Tests/PlayMode/DB.Foundation.PlayTests.asmdef`）。`System.AOT` 不引用 HotUpdate，其编译不运行生成器（也无妨：其中没有 `[UI]` 页面）。
- 自门控保证无害：生成器只扫描当前编译程序集内的 `F13.GameLogic.UI` 命名空间，对不相关程序集直接返回；`[UI]` 页面只存在于 HotUpdate，故注册表只生成一次、编译进 HotUpdate 程序集。

### 1.4 生成代码的运行期消费

- **注册表（编译期内存生成，不入磁盘）**：`Assets/Scripts/GameLogic/GameLogicEntry.cs:70-71` 在组合根调用 `UIRegistryGenerated.RegisterAll(uiRegistry)`；测试同样直接使用（`Assets/Scripts/Tests/GameLogic/T05UIServiceTests.cs:186-190` 等）。生成类为 `public`，随 HotUpdate 程序集编译产出，磁盘上刻意不存在 `UIRegistryGenerated.cs`（见 `Docs/archive/next-client/T06-source-generator-research.md` Implementation Status）。
- **Bindings（Editor 工具写出、随代码提交）**：`Assets/Scripts/Editor/UI/UIScriptGenerator.cs` 提供菜单 `Assets/Generate UI Page` / `Assets/Generate UI View`，读取 Prefab 根上的 `UIBindingHost`（序列化 `Object[]`），按命名规则（`_txt`/`_btn` 等前后缀）生成 `Assets/Scripts/GameLogic/UI/Generated/<Name>Bindings.g.cs`、页面骨架（`[UI(Address="…")]` + 嵌套 `Args : IUIArgs<…>`）并把绑定顺序写回 Prefab。`Bindings.g.cs` 是**真实提交的普通源码**（git 中已有 `UIMainBindings.g.cs`、`UITestBindings.g.cs` 等十余个）。
- **运行期零反射**：生成器产出的 `RegisterAll` 直接 `registry.Register(new UIPageRegistration(typeof(Page), address, layer, …, new PageFactory()))`；`PageFactory.Create` 中 `new Page(); page.Initialize(root, new PageBindings((UIBindingHost)bindings), pageContext)`。契约类型（`UIAttribute`/`UIBase`/`UIRegistry`/`IUIPageFactory`/`UIPageRegistration`）位于 `Assets/Scripts/Framework/System/UI/`（System.AOT 程序集，命名空间 `DB.System`）。

### 1.5 HybridCLR 热更 DLL 编译

client 的 HotUpdate 程序集经 HybridCLR 编译：`Packages/com.code-philosophy.hybridclr@8.13.0/Editor/Commands/CompileDllCommand.cs:24` 使用 **`PlayerBuildInterface.CompilePlayerScripts(scriptCompilationSettings, buildDir)`**（Unity 官方 player 脚本编译接口）。注册表能否进入热更 DLL 取决于该编译是否带分析器；client 的实证是：SG 接入后 Android 构建**通过了 HotUpdate 编译阶段**，后续才被 YooAsset 缺失 Prefab 阻断（`Docs/archive/next-client/T07-service-manager-access.md:552-556`）——若生成器未参与，`UIRegistryGenerated` 未定义会直接编译失败。据此判断分析器参与 player 脚本编译（该点官方手册未显式声明，[INFERENCE]，依据：HybridCLR 走 `CompilePlayerScripts` + client 构建记录）。

### 1.6 小结（机制清单）

| 环节 | client 的做法 | 证据 |
|---|---|---|
| 生成器源码 | `Tools/`（Assets 外）独立 csproj，netstandard2.0 + CodeAnalysis 3.8 | `Tools/DB.UI.SourceGenerator/DB.UI.SourceGenerator.csproj` |
| 进入编译 | DLL 提交在 Assets 内 + `.meta` 打 `RoslynAnalyzer` 标签 | `Assets/Scripts/GameLogic/Analyzers/DB.UI.SourceGenerator.dll.meta` |
| 作用域 | 目录位于含 asmdef 的文件夹下 → 仅该 asmdef 程序集及其引用者 | `.gitignore:44-46`、`GameLogic.HotUpdate.asmdef`、`System.AOT.asmdef` |
| 注册表消费 | 组合根 `UIRegistryGenerated.RegisterAll(registry)` | `GameLogicEntry.cs:70-71` |
| Bindings | Editor 菜单工具写 `Bindings.g.cs` 并提交 | `Assets/Scripts/Editor/UI/UIScriptGenerator.cs` |

---

## 2. Unity 2022.3 官方约定（UPM 包内分发依据）

以下引用官方手册原文（2022.3 版）：<https://docs.unity3d.com/2022.3/Documentation/Manual/roslyn-analyzers.html>

### 2.1 官方安装/接入流程（标签机制）

手册 "Source generators" 一节给出的标准步骤：

1. 创建 .NET Standard 2.0 类库；安装 `Microsoft.CodeAnalysis` NuGet 包——手册原文 "Your source generator **must use Microsoft.CodeAnalysis 3.8** to work with Unity"。
2. 编译 Release，取 `bin/Release/netstandard2.0/<Name>.dll`。
3. 把该 DLL 复制进 Unity 工程（**Assets 文件夹内**；对包内分发同样适用，见 §2.2）。
4. Plugin Inspector：**Select platforms → 关闭 Any Platform**；**Include Platforms → 关闭 Editor 与 Standalone**。
5. **Asset Labels → 新建并指派 `RoslynAnalyzer` 标签**——"This label must match exactly and is case sensitive"。手册原文："Unity recognizes the RoslynAnalyzer label and treats assets with this label as Roslyn Analyzers or source generators. When you assign the label to an analyzer, Unity recompiles scripts within the scope of the analyzer and analyzes the code in those scripts."

另：手册明确 **Unity 不支持直接通过 NuGet 安装分析器**（"Unity doesn't support the installation of Roslyn Analyzers or source generators through NuGet directly"），需下载 nupkg 解包后取 DLL 放入工程再打标签。生成器 DLL 被注入多个程序集时 Unity 会打印警告（同名类型冲突），解决方式是让生成代码 `internal` 或名称唯一——client 的 `UIRegistryGenerated` 为 public 但每程序集唯一生成、无冲突。

### 2.2 分析器作用域（决定包内放哪的关键）

手册 "Analyzer scope" 一节原文：

> Unity applies analyzers to all assemblies in your project's Assets folder, or in any subfolder whose parent folder doesn't contain an assembly definition file. If an analyzer is in a folder that contains an assembly definition, or a subfolder of such a folder, the analyzer only applies to the assembly generated from that assembly definition, and to any other assembly that references it.

即：

- DLL 位于 Assets 根或「父目录无 asmdef 的任意子目录」→ **作用于工程全部程序集**；
- DLL 位于「含 asmdef 的目录或其子目录」→ 仅作用于**该 asmdef 的程序集 + 引用它的程序集**。

手册并明确指出包可以借此提供分析器："a package can supply analyzers that only analyze code related to the package, which can help package users to use the package API correctly."——包的目录同样适用该规则，无需 package.json 里任何特殊字段。

### 2.3 Additional files 约定

2022.3 手册**没有**独立的 additional files 页面（访问 `https://docs.unity3d.com/2022.3/Documentation/Manual/roslyn-analyzers-additional-files.html` 返回 404，已实测）；当前手册（canonical 6000.5，<https://docs.unity3d.com/Manual/roslyn-analyzers-additional-files.html>）的约定是：

- 附加文件必须带 `.additionalfile` 扩展名，且位于 **Assets 或其子目录**内才会被识别；
- 命名格式 `Filename.[Analyzer Name].additionalfile`，`[Analyzer Name]` 大小写敏感、必须等于目标分析器名；`Filename` 不能含 `.`；
- 按分析器名对每个程序集过滤附加文件，分析器通过 `context.AdditionalFiles` 读取（也给出 `Compilation.ScriptCompilerOptions.RoslynAdditionalFilePaths` 供 Editor 代码读取）。

client 的早期设计文档 `Docs/archive/next-client/T06-source-generator-research.md` 曾规划 `*.UISourceGenerator.additionalfile` 清单（Prefab 元数据进生成器），命名与当前手册格式一致；但**当前实现未使用附加文件**（生成器只读编译语义模型）。

### 2.4 版本要求汇总

| 要求 | 官方出处 |
|---|---|
| 生成器目标框架 .NET Standard 2.0 | 2022.3 `roslyn-analyzers.html` "Source generators" 步骤 1 |
| 引用 Microsoft.CodeAnalysis **3.8** | 同上（must use 3.8） |
| 标签名精确为 `RoslynAnalyzer`（大小写敏感） | 同上 步骤 11 |
| 作用域由 DLL 目录与 asmdef 相对位置决定 | 同上 "Analyzer scope" |
| NuGet 直装不支持 | 同上 "Installing an existing Roslyn analyzer or source generator" |

---

## 3. cascade 推荐打包布局

### 3.1 布局

镜像 client 的「源码在包外、DLL 入库」先例，同时符合官方标签机制：

```
cascade/                                    # 仓库根
├── Tools/                                  # 生成器源码工程（Assets/包之外，client 先例）
│   ├── Cascade.SourceGenerator/
│   │   ├── Cascade.SourceGenerator.csproj  # netstandard2.0 + Microsoft.CodeAnalysis.CSharp 3.8.0
│   │   └── UISourceGenerator.cs            # 元数据名自门控：Cascade.System.UIAttribute / UIBase
│   └── Cascade.SourceGenerator.Tests/      # CSharpGeneratorDriver 冒烟测试（client 先例）
└── Packages/com.source27.cascade/
    ├── package.json                        # 无需分析器相关字段
    ├── Runtime/
    │   ├── Cascade.System.asmdef           # AOT 固化层：UIBase/UIAttribute/UIRegistry/IUIPageFactory/UIBindingHost
    │   └── UI/…
    ├── Editor/
    │   ├── Cascade.Editor.asmdef           # includePlatforms:["Editor"]：Prefab→Bindings.g.cs 菜单工具
    │   └── UIScriptGenerator.cs
    └── Roslyn/                             # 生成器编译产物（无 asmdef，作用域=全工程）
        ├── Cascade.SourceGenerator.dll
        └── Cascade.SourceGenerator.dll.meta   # labels:[RoslynAnalyzer]；Any 关、Editor/Standalone 关
```

要点：

- **`Roslyn/` 目录（或包根）不放置任何 asmdef**：按 §2.2 规则，DLL 处于「父目录无 asmdef 的子目录」，分析器作用于**工程全部程序集**——这是让生成器能在消费方（示例工程热更层）页面程序集编译时运行的唯一包内位置（若放 `Editor/` 且 `Editor/` 含 `Cascade.Editor.asmdef`，作用域会被收窄到 Cascade.Editor 及其引用者，消费方页面程序集将收不到生成器）。
- **全工程作用域无害**：生成器按元数据名自门控（找不到 `Cascade.System.UIAttribute`/`UIBase` 即返回），且只扫描当前编译程序集内页面命名空间（client 的 `UISourceGenerator.cs` 同款逻辑）。
- **meta 随 DLL 提交**（UPM 包内 .meta 必须入库）：`labels: [RoslynAnalyzer]`；平台按官方步骤 Any 关、Editor/Standalone 关（client 的 meta 是 Any 关、Editor 开，两者都能让分析器在编辑器编译中生效，平台开关只影响运行期插件打包，推荐按官方步骤写死）。
- **`.gitignore` 放行**：`*.csproj` 全局忽略 + `!/Tools/Cascade.SourceGenerator/*.csproj`（+ Tests），与 client `.gitignore:44-46` 一致；DLL 本体在包内正常提交。

### 3.2 生成代码的运行期消费（沿用 client 模式）

| 产物 | 生成方式 | 消费方式 |
|---|---|---|
| `UIRegistryGenerated`（注册表+Factory） | 编译期 `AddSource`，编译进**消费方程序集**，不入磁盘 | 消费方组合根：`UIRegistryGenerated.RegisterAll(new UIRegistry())`（client `GameLogicEntry.cs:70-71`） |
| `<Name>Bindings.g.cs` | 包内 `Cascade.Editor` 菜单工具读 Prefab `UIBindingHost` 写出 | 随消费方源码提交、编译进消费方程序集；`UIPage.Initialize(root, new XxxBindings((UIBindingHost)bindings), pageContext)` |
| 页面骨架（`[UI]` + `Args`） | 同上菜单工具 | 消费方手写扩展 |

- 契约类型放 `Runtime/`（Cascade.System，AOT 固化层）；消费方页面程序集 asmdef 引用 `Cascade.System` 即可——**无需任何 asmdef 分析器引用**（client 的 `GameLogic.HotUpdate.asmdef`/`System.AOT.asmdef` 均无分析器字段）。
- HybridCLR 场景：生成器参与 player 脚本编译（§1.5，[INFERENCE]），注册表进入热更 DLL；组合根调用 `RegisterAll` 完成显式注册，运行期零反射、不扫描程序集。

### 3.3 构建与发布流程

1. `dotnet build Tools/Cascade.SourceGenerator -c Release`（引用 CodeAnalysis 3.8，官方强制版本）。
2. 复制 `bin/Release/netstandard2.0/Cascade.SourceGenerator.dll` 到 `Packages/com.source27.cascade/Roslyn/`（Unity 自带 Roslyn，只需生成器单 DLL——client 的 Analyzers 目录仅有 24.5KB 单 DLL 可证）。
3. 提交 DLL 与 `.dll.meta`（meta 一旦入库即固定 `RoslynAnalyzer` 标签与平台开关，消费方开箱即用）。
4. 修改生成器后重跑 1–3；`Cascade.SourceGenerator.Tests` 用 `CSharpGeneratorDriver` 在 Unity 外冒烟（client `Tools/DB.UI.SourceGenerator.Tests/Program.cs` 先例）。

### 3.4 可选增强：附加文件

若生成器将来需要 Prefab 元数据清单（如 client T06 规划），按 §2.3 约定：消费方在 Assets 内提供 `Xxx.CascadeUISourceGenerator.additionalfile`（`[Analyzer Name]` 与生成器类名一致、大小写敏感）；2022.3 手册无独立页面（404 已实测），以当前手册为准并在包文档中注明。

---

## 4. 参考链接

- Unity 2022.3 Manual "Roslyn analyzers and source generators"：<https://docs.unity3d.com/2022.3/Documentation/Manual/roslyn-analyzers.html>
- Unity Manual（canonical 6000.5）"Additional files for Roslyn analyzers and source generators"：<https://docs.unity3d.com/Manual/roslyn-analyzers-additional-files.html>（2022.3 无此页，404 实测）
- Microsoft `ISourceGenerator`：<https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.isourcegenerator>
- client 实证文件：`E:\Projects\client\Tools\DB.UI.SourceGenerator\**`、`Assets\Scripts\GameLogic\Analyzers\DB.UI.SourceGenerator.dll(.meta)`、`Assets\Scripts\Editor\UI\UIScriptGenerator.cs`、`Assets\Scripts\GameLogic\GameLogicEntry.cs`、`Docs\archive\next-client\T06-source-generator-research.md`、`T07-service-manager-access.md`
- HybridCLR 8.13.0：`E:\Projects\client\Packages\com.code-philosophy.hybridclr@8.13.0\Editor\Commands\CompileDllCommand.cs`
