# Joy

Joy 是由 **bddfs** 四人团队共同开发的游戏项目，目前名称为暂定代号。

## 当前阶段

当前版本为 **v0.3.0-art.1 美术与界面候选试玩版**，已将 P1–P5 接入客户端：七节点城市经营图、固定旅馆现场、五槽卡牌、统一实物缩图，以及长预览、事实反馈和日结明细。提供 100% / 125% / 150% 文字缩放、灰度与静态设置。实现范围和仍待人工评审的项目见 [美术实施记录](docs/art/implementation-v0.1.md)，客户端截图与隐藏信息配对结果见 [QA 验证记录](output/art-ui-v01/qa/validation.json)。候选接入和自动检查不代表已经获得最终人工审美批准。

本轮核心自动回归 **928 项通过、0 失败**；客户端 QA 脚本覆盖 59 个截图状态和 9 组未知 A/B 配对，具体执行结果以验证记录为准。

已根据 [需求 v0.3 的 2026-09-15 修订](docs/requirements/Joy_需求规格说明书_v0.3.md) 建立 **Godot 4.7.2 .NET + C# M2 单城可玩验证版**：城市经营、两队调度、镜中住客 A/B 推理、固定五卡槽、唯一封装工具、灯具处置与自动续玩。

固定七节点、五日目标，支持管理和混合手操路线。23 条实际引擎日程已核对逐日资金、工具余量与交接；2026-09-16 异动修正后自动回归 817 项通过，实际结算与预览统一为达到阈值后回落到 4。三城连续生成、完整成长内容和正式发行尚未完成，首次玩家推理与玩法取舍仍待试玩。具体范围和验收记录见 [M2 实施说明](docs/implementation-m2.md)；[旧版记录](docs/implementation-v0.3.md) 仅作为历史资料。

接续开发加入第二套装备组合原型 **誊写护符夹 + 正常搜查**：安全值班室的便条后有一件可选实物。已持有、护符为 0 且有墨水时，成功正常搜查会在规则结算前消耗 1 墨水及 1 装备次数，护符 +1；整局最多 2 次，模仿不触发。可以拒收、放回房间或归库后在下一日为手操队配装。加入后自动回归 **877 项通过、0 失败**，23 条原日程结果不变。旧 v2 档不补发装备，请开新 M2 局体验投放。此原型尚未经过真人平衡验证，不代表完整成长系统或 AC-29 已验收。

已确认方向为单人、Steam Windows/macOS、Godot .NET 与 C#。团队不设固定细分岗位；具体玩法边界、验证范围和待定参数以文档标注为准。

## 需求文档

- 最新策划：[异常回收公司详细策划案 v0.2](docs/design/异常回收公司详细策划案_v0.2.md) · [PDF 审阅版](output/pdf/异常回收公司详细策划案_v0.2.pdf)。包含首个 Demo 后的玩法样例、资源账和待验证方案。
- 当前需求：[Joy 需求规格说明书 v0.3](docs/requirements/Joy_需求规格说明书_v0.3.md) · [PDF 审阅版](output/pdf/Joy_需求规格说明书_v0.3.pdf)。采用“2026-09-15 策划 v0.2 对齐修订”，共 118 条需求、42 个验收场景；第 15 节给出 M2 内容、校正日程及验收映射。
- 历史版本：[需求 v0.2](docs/requirements/Joy_需求规格说明书_v0.2.md)；[原始策划案 v0.1](docs/design/异常回收公司详细策划案_v0.1.md)。

策划案与需求文档分别编号。资源账以需求第 15 节的修正为准；本版的实际测试证据与仍未完成的验收分别记录在 [M2 实施说明](docs/implementation-m2.md)，自动测试不替代首次真人推理与玩法取舍观察。

## 游戏百科

[Joy Wiki · 异常回收公司百科](docs/wiki/README.md) 收录当前 M2 已定义的地点、房间、异常、卡牌、实物、合同、经营系统和路线参考，共 9 页，含完整剧透。百科将当前可用内容、历史 Legacy 与完整版本规划分别标注，并记录文档和实现之间的差异。

- [地点与房间](docs/wiki/locations.md) · [异常与威胁](docs/wiki/anomalies.md) · [卡牌与基础行动](docs/wiki/cards.md)
- [物品与工具](docs/wiki/items.md) · [合同与事务](docs/wiki/contracts.md) · [经营、状态与存档](docs/wiki/mechanics.md)
- [入门与路线参考](docs/wiki/walkthrough.md) · [版本、来源与规划](docs/wiki/sources.md)

[下载完整百科压缩包](output/Joy_Wiki_M2_v1.0.zip)，解压后从 `Joy_Wiki/README.md` 开始阅读。

## 团队协作

- 四名开发者使用 ChatGPT 辅助讨论与开发，通过 GitHub 协作。
- 已确认的设计与技术决策整理到仓库，作为共同的实现依据。
- 设计变更先记录并评估影响，再同步到实现。

## 美术与音频

- [美术与界面需求规格 v0.1](docs/requirements/Joy_美术与界面需求规格说明书_v0.1.md) · [PDF 审阅版](output/pdf/Joy_美术与界面需求规格说明书_v0.1.pdf)。独立配套游戏需求 v0.3，包含 32 条需求、15 个验收场景及首轮资产状态清单；明确已知信息、经营状态、卡牌、物品和日结的呈现边界。规范仍为评审稿；已实施和已验证的部分由 [本轮实施记录](docs/art/implementation-v0.1.md) 单独列明，不能据此视为全部 AT 验收通过。
- [整体美术风格提案 v0.1](docs/design/美术风格提案_v0.1.md) · [高清风格样板](output/art/joy-art-board-v0.1.png)。以“深空星图 × 都市怪谈 × 公司档案”为方向，包含配色、地图状态、现场、卡牌、动效及生成提示词；当前为供讨论的概念提案，尚未冻结，也不是实机界面。
- 美术主要由 AI 生成，或以团队手绘作品为基础进行 AI 润色。
- 本轮素材的稳定 ID、来源、提示词、尺寸、校验和权利边界见 [资产清单](docs/art/assets.json)。候选资源仍待团队审美批准；发布包附同一清单及 AI 素材权利说明。
- 音频初期以简单的背景音乐满足基本体验。

## 运行说明

本轮以本地 **Joy-v0.3.0-art.1-windows-x64.zip** 交付试玩，实现同步到 [codex/art-ui-v01 分支](https://github.com/bddfs/Joy/tree/codex/art-ui-v01)。取得 ZIP 后完整解压并运行 `Joy.exe`，保留目录中的全部文件；旁附的 `SHA256SUMS.txt` 用于校验。Windows 包采用自包含运行时，无需安装 Godot、.NET SDK 或另装运行时。范围和存档说明见 [本版试玩说明](docs/releases/v0.3.0-art.1.md)，打包与解压验证记录见 [实施说明](docs/art/implementation-v0.1.md)；[M2.1 试玩说明](docs/releases/v0.3.0-m2.1.md) 保留为历史记录。

在当前 Windows 便携环境，双击仓库上一级的 **Play-Joy.cmd** 即可编译并启动，**Test-Joy.cmd** 运行回归检查；通过 **Open-Godot.cmd** 在编辑器打开工程后按 F6/F5 运行也可以。

团队成员安装 Godot **4.7.2 .NET** 与 .NET SDK **10.0.401**，将 `project.godot` 导入 Godot。详细环境见 [开发环境](docs/development-environment.md)。普通 Godot 不支持本项目的 C# 脚本。

```powershell
# 在 Joy 仓库根目录、正确 SDK 的终端中
dotnet build Joy.csproj
dotnet run --project Tests/Joy.Tests.csproj
# Windows 便携环境：自动选择旁边 tools 中的 SDK 和引擎
powershell -ExecutionPolicy Bypass -File tools/dev.ps1 -Action Run
# 美术客户端 QA：构建、回归、59 个截图状态与 9 组未知 A/B 像素配对
powershell -ExecutionPolicy Bypass -File tools/validate-art.ps1
# 也可指定已安装的 Godot .NET 编辑器
# ./tools/validate-art.ps1 -GodotPath C:/Godot/Godot_console.exe
```

美术 QA 默认写入 `output/art-ui-v01/qa`，汇总为 [validation.json](output/art-ui-v01/qa/validation.json)，每张 PNG 配有文字和按钮几何记录。截图夹具与正式存档隔离；几何和配对检查仍需结合人工目视与真实操作复核。

游戏自动保存到 Godot 的 `user://profile-m2/save.json`，上一份为 `save.json.bak`。美术版继续使用并兼容已有 **v2 存档**，历史 `user://profile` 目录保留、不混读。退出后恢复最近成功的完整检查点；本轮确认奖励之后的新检查点优先于轮初。界面会显示实际成功保存的位置，测试档与试玩档分开。旧档不会补发后来加入的实物，体验护符夹投放请开新局。

标题可选择灯具公开报价 6 / 2，并独立开启传播压力。默认场景便于核对需求第 15 节的五日资源安排。计划中的手操队可在“工具配装”页调整随行实物；选定牌和基础动作后，在常驻预览区执行。

代码结构：`Core/` 为不依赖 Godot 的规则与状态，`Scripts/` 为桌面界面，`Scenes/Main.tscn` 为入口，`Tests/` 为可直接运行的验收检查。SDK 版本由 `global.json` 固定；客户端目标框架为 `net8.0`，测试运行器使用本环境的 `net10.0`。

## Windows 发布包构建

安装与编辑器匹配的 **Godot 4.7.2 .NET 导出模板**，提交游戏代码和导出配置后，在仓库根目录执行：

```powershell
powershell -ExecutionPolicy Bypass -File tools/release.ps1
# 非本机便携目录可指定编辑器及工程外的输出目录
# ./tools/release.ps1 -GodotPath C:/Godot/Godot_console.exe -OutputRoot C:/JoyBuilds
```

脚本从仓库开始最多查找五层目录中的便携 SDK 与编辑器，支持本地工作树；也可通过 `-GodotPath` 或 `GODOT4` 指定引擎。它执行回归检查、资源导入和 Windows x64 Release 导出，附带运行时、中文字体、许可、试玩说明及源码提交信息，最后生成 ZIP 和 `SHA256SUMS.txt`。资产清单位于包内 `Art/assets.json`，AI 素材来源与权利说明位于 `Licenses/AI-Art-NOTICE.txt`；不复制开发缓存或整套开发工具。

每次使用独立输出目录；找到便携 SDK 时默认写入其工作区的 `builds`，否则写入仓库上一级的 `builds`。发布前还需将 ZIP 解压到新目录，验证启动、基本操作及续玩；脚本本身不会发布 GitHub Release。
