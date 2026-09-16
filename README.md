# Joy

Joy 是由 **bddfs** 四人团队共同开发的游戏项目，目前名称为暂定代号。

## 当前阶段

已根据 [需求 v0.3 的 2026-09-15 修订](docs/requirements/Joy_需求规格说明书_v0.3.md) 建立 **Godot 4.7.2 .NET + C# M2 单城可玩验证版**：城市经营、两队调度、镜中住客 A/B 推理、固定五卡槽、唯一封装工具、灯具处置与自动续玩。

固定七节点、五日目标，支持管理和混合手操路线。23 条实际引擎日程已核对逐日资金、工具余量与交接；2026-09-16 异动修正后自动回归 817 项通过，实际结算与预览统一为达到阈值后回落到 4。三城连续生成、完整成长内容和正式发行尚未完成，首次玩家推理与玩法取舍仍待试玩。具体范围和验收记录见 [M2 实施说明](docs/implementation-m2.md)；[旧版记录](docs/implementation-v0.3.md) 仅作为历史资料。

已确认方向为单人、Steam Windows/macOS、Godot .NET 与 C#。团队不设固定细分岗位；具体玩法边界、验证范围和待定参数以文档标注为准。

## 需求文档

- 最新策划：[异常回收公司详细策划案 v0.2](docs/design/异常回收公司详细策划案_v0.2.md) · [PDF 审阅版](output/pdf/异常回收公司详细策划案_v0.2.pdf)。包含首个 Demo 后的玩法样例、资源账和待验证方案。
- 当前需求：[Joy 需求规格说明书 v0.3](docs/requirements/Joy_需求规格说明书_v0.3.md) · [PDF 审阅版](output/pdf/Joy_需求规格说明书_v0.3.pdf)。采用“2026-09-15 策划 v0.2 对齐修订”，共 118 条需求、42 个验收场景；第 15 节给出 M2 内容、校正日程及验收映射。
- 历史版本：[需求 v0.2](docs/requirements/Joy_需求规格说明书_v0.2.md)；[原始策划案 v0.1](docs/design/异常回收公司详细策划案_v0.1.md)。

策划案与需求文档分别编号。资源账以需求第 15 节的修正为准；本版的实际测试证据与仍未完成的验收分别记录在 [M2 实施说明](docs/implementation-m2.md)，自动测试不替代首次真人推理与玩法取舍观察。

## 团队协作

- 四名开发者使用 ChatGPT 辅助讨论与开发，通过 GitHub 协作。
- 已确认的设计与技术决策整理到仓库，作为共同的实现依据。
- 设计变更先记录并评估影响，再同步到实现。

## 美术与音频

- 美术主要由 AI 生成，或以团队手绘作品为基础进行 AI 润色。
- 音频初期以简单的背景音乐满足基本体验。

## 运行说明

直接试玩可下载 [Windows 64 位预发行包](https://github.com/bddfs/Joy/releases/tag/v0.3.0-m2.1)，完整解压后运行 `Joy.exe`。包内已带运行时，无需安装开发环境。范围和存档说明见 [本版试玩说明](docs/releases/v0.3.0-m2.1.md)。

在当前 Windows 便携环境，双击仓库上一级的 **Play-Joy.cmd** 即可编译并启动，**Test-Joy.cmd** 运行回归检查；通过 **Open-Godot.cmd** 在编辑器打开工程后按 F6/F5 运行也可以。

团队成员安装 Godot **4.7.2 .NET** 与 .NET SDK **10.0.401**，将 `project.godot` 导入 Godot。详细环境见 [开发环境](docs/development-environment.md)。普通 Godot 不支持本项目的 C# 脚本。

```powershell
# 在 Joy 仓库根目录、正确 SDK 的终端中
dotnet build Joy.csproj
dotnet run --project Tests/Joy.Tests.csproj
# Windows 便携环境：自动选择旁边 tools 中的 SDK 和引擎
powershell -ExecutionPolicy Bypass -File tools/dev.ps1 -Action Run
```

游戏自动保存到 Godot 的 `user://profile-m2/save.json`，上一份为 `save.json.bak`。M2 使用 v2 存档，历史 `user://profile` 目录保留、不混读。退出后恢复最近成功的完整检查点；本轮确认奖励之后的新检查点优先于轮初。界面会显示实际成功保存的位置，测试档与试玩档分开。

标题可选择灯具公开报价 6 / 2，并独立开启传播压力。默认场景便于核对需求第 15 节的五日资源安排。计划中的手操队可在“工具配装”页调整随行实物；选定牌和基础动作后，在常驻预览区执行。

代码结构：`Core/` 为不依赖 Godot 的规则与状态，`Scripts/` 为桌面界面，`Scenes/Main.tscn` 为入口，`Tests/` 为可直接运行的验收检查。SDK 版本由 `global.json` 固定；客户端目标框架为 `net8.0`，测试运行器使用本环境的 `net10.0`。

## Windows 发布包构建

安装与编辑器匹配的 **Godot 4.7.2 .NET 导出模板**，提交游戏代码和导出配置后，在仓库根目录执行：

```powershell
powershell -ExecutionPolicy Bypass -File tools/release.ps1
# 非本机便携目录可指定编辑器及工程外的输出目录
# ./tools/release.ps1 -GodotPath C:/Godot/Godot_console.exe -OutputRoot C:/JoyBuilds
```

脚本执行回归检查、资源导入和 Windows x64 Release 导出，附带运行时、中文字体、许可、试玩说明及源码提交信息，最后生成 ZIP 和 `SHA256SUMS.txt`。每次使用独立目录，默认位于仓库上一级的 `builds`。发布前还需将 ZIP 解压到新目录，验证启动、基本操作及续玩；脚本本身不会发布 GitHub Release。
