# Joy · 异常回收公司

当前分支 `codex/grid-card-prototype` 是 **v0.4.0-grid.1 网格卡牌玩法原型**。

[下载 Windows 试玩版](https://github.com/bddfs/Joy/releases/tag/v0.4.0-grid.1)：完整解压 ZIP 后运行 `Joy.exe`。发布页附 SHA-256 校验文件，包内自带运行时，无需安装开发环境。

## 本版方向

经营一家异常回收公司，把有限的角色和异常能力分配到公司岗位与外勤队伍。角色拥有个人牌组和两条成长专精；城市与现场都采用网格，现场所有主动操作都通过卡牌进行。探索、战斗与收容共享位置、手牌和资源。

- 三名角色：测绘员林舟、灯光工程师许灯、收容官陈墨；每次两人出勤，一人留守。
- 两层地图：7×5 城市街区与 9×7 现场，包含真实通路、阻断、地点、位置和范围。
- 三处任务：旅馆、医院、源头；先调查、再处置或收容，成功撤离后成果才能兑现。
- 全卡牌现场：常驻基础与交互牌保证基本操作，个人牌组提供机动、控制、防护与组合。
- 三件异常资产：回声铃、折叠衣柜、食痛者，留库与随队效果互斥。
- 独立自动存档：`user://grid-prototype-v1/save.json`，每个成功操作后保存，保留一份备份。

本版是固定单城的玩法验证，不代表三城内容、正式平衡或完整剧情已经完成。试玩方法见 [试玩说明](docs/releases/v0.4.0-grid.1.md)，验证证据见 [本轮实施记录](docs/implementation-grid-v0.4.md)。

## 本地运行

在工作区顶层打开 `Play-Joy-Grid.cmd`，或在本工程目录执行：

```powershell
powershell -ExecutionPolicy Bypass -File tools/dev.ps1 -Action Run
```

开发环境继续使用现有 Godot 4.7.2 .NET 与便携 .NET SDK。`Core/GridPrototype/` 是独立规则核心，`Scripts/GridPrototype/` 是新界面，`Scenes/GridPrototype.tscn` 是入口。

```powershell
powershell -ExecutionPolicy Bypass -File tools/dev.ps1 -Action Test
powershell -ExecutionPolicy Bypass -File tools/dev.ps1 -Action Build
```

旧 M2 核心与界面仍保留在工程内，旧版存档 `profile-m2` 不读取、不迁移。新分支以 `codex/art-ui-v01` 为基础；新设计以本轮已确认的五项方向为准，旧文档中非卡牌基础动作等描述属于历史方案。

## 历史资料

- [策划 v0.2](docs/design/异常回收公司详细策划案_v0.2.md)
- [需求 v0.3](docs/requirements/Joy_需求规格说明书_v0.3.md)
- [美术与界面需求 v0.1](docs/requirements/Joy_美术与界面需求规格说明书_v0.1.md)
- [M2 实施记录](docs/implementation-m2.md)
- [美术实施记录](docs/art/implementation-v0.1.md)
- [M2 Wiki](docs/wiki/README.md)

本版复用已经生成的城市、场景与档案插图，素材来源与许可保留在 [资产清单](docs/art/assets.json) 和 `Licenses/`。团队为 bddfs 四人协作，目标仍为单人 Windows/macOS；本轮只验证 Windows。
