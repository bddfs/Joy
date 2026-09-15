# Joy 开发环境

当前选择：Godot .NET + C#。计划支持 Windows 和 macOS；先在 Windows 建立工程，并在开发期间安排 Mac 真机测试。

开发工具和版本约束已建立；2026-09-15 已创建正式 Godot 工程，并按最新需求 v0.3 第 15 节更新为 M2 单城可玩验证版。模块、存档路径与当前范围见 [M2 实施记录](implementation-m2.md)。

## 团队统一版本

| 项目 | 版本与要求 |
| --- | --- |
| Godot 编辑器 | **4.7.2 stable .NET**，注意选择支持 C# 的版本 |
| .NET SDK | **10.0.401 stable**，由仓库根目录的 `global.json` 固定 |
| 导出模板 | 首次打包时安装 **4.7.2 .NET** 模板 |
| 编辑器 | 建议 VS Code + Microsoft C# 扩展 `ms-dotnettools.csharp` |
| 版本管理 | Git，使用各自的 GitHub 身份 |

Godot 和 SDK 需要团队统一。VS Code、扩展和 Git 的补丁版本不必严格一致。不要将预览版 SDK 作为项目环境；升级 Godot 或 SDK 时一起更新文档、版本约束，并验证编译与运行。

SDK 版本不等于游戏目标框架。Godot 4.7.2 新建桌面 C# 工程默认使用 `net8.0`。Windows 环境已验证 SDK 10.0.401 可以编译该项目，Godot 可以在随 SDK 安装的 .NET 10 运行时加载它。独立的测试程序需要另外确认其目标框架对应的运行时。

## 每位开发者的准备步骤

1. 安装或解压官方的 Git、VS Code、.NET SDK 10.0.401，以及 Godot 4.7.2 .NET。
2. 用自己的 GitHub 账号完成 Git 认证，将 `bddfs/Joy` 克隆到自己的 `JoyGame/Joy`。
3. 在 VS Code 安装 `ms-dotnettools.csharp`。在 Godot 的编辑器设置中，将 Dotnet → Editor → External Editor 设为 Visual Studio Code。
4. 在仓库目录执行 `dotnet --version`，应输出 `10.0.401`；检查 GitHub 远端访问与个人提交姓名、邮箱。
5. 导入仓库根目录的 `project.godot`；先构建 C# 工程，再按 F5 运行。Windows 便携环境也可双击上一级 `Play-Joy.cmd`。运行 `dotnet run --project Tests/Joy.Tests.csproj` 执行纯 C# 回归检查。

在 Windows 上使用便携工具时，可以采用如下布局：

```text
JoyGame/
  Joy/       # Git 仓库
  tools/     # 本机开发工具
  builds/    # 导出产物
```

Mac 成员使用 macOS 版 Godot .NET 和对应架构的 SDK。Windows 的便携 EXE 与启动脚本仅用于 Windows；四个人共享的是仓库内容和工具版本。

## 文件管理

- 提交源代码、场景、资源、项目文件、版本约束和团队文档。
- `.gitignore` 已排除 Godot 缓存、.NET 编译产物、导出目录和常见本地配置。
- `.gitattributes` 统一文本换行，并将 PDF、图片、音频标记为二进制。
- `.editorconfig` 统一编码、缩进和行末格式。
- 工具安装目录和压缩包放在仓库外；不要把整个 `JoyGame` 初始化为另一个 Git 仓库。

## 官方下载与说明

- [Godot 4.7.2 固定版本下载](https://godotengine.org/download/archive/4.7.2-stable/)
- [.NET 10 SDK 下载](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- [VS Code 下载](https://code.visualstudio.com/download)
- [Git 下载](https://git-scm.com/install/)
- [Godot C# 环境要求](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_basics.html)
- [Godot 导出说明](https://docs.godotengine.org/en/stable/tutorials/export/exporting_projects.html)

本地 Windows 环境已通过 C# 编译、无窗口导入、实际图形启动和基础界面点击验证。macOS 真机、完整交互回归、导出模板与 Steam 发布仍需后续验证。
