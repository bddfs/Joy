# 美术与界面 v0.1 候选实现与需求追踪

日期：2026-09-24。对象：`art-ui-v01` 工作目录的 `0.3.0-art.1` 候选实现。依据：[美术与界面需求 v0.1](../requirements/Joy_美术与界面需求规格说明书_v0.1.md)、[游戏需求 v0.3](../requirements/Joy_需求规格说明书_v0.3.md)。美术提案/样板固定来源为 `74f2aa72eea1d2549db7d5270f40a32dec516cf8`；游戏 M2 原始基线为 `693307638a7d9521b8ee185628d8d685b66e15b2`。

这是实现与证据矩阵，不是四人团队验收签字。视觉风格、恐怖强度、最终规范、最低规格和 macOS 尚未获准或验证。单城样例不代表完成三城远征。原始需求文档保留其发布时状态，本文件另记本次实现。

## 证据与状态口径

- **核心已测**：本轮便携 .NET SDK 运行 `Tests/Joy.Tests.csproj`，**928 条通过、0 失败**；其中新增 [ArtPresentationTests](../../Tests/ArtPresentationTests.cs) 51 条。该数字不代替客户端视觉/交互验收，也不预先覆盖之后的修改。
- **候选已接入**：资源或控件已存在，能定位实现；仍需最终构建、截图或操作证据。
- **部分覆盖**：条款有明确实现，但规定的全部状态/流程尚未形成证据。
- **待人工评审/待目标环境复核**：没有用自动测试、代码存在或截图文件数量冒充通过。
- Windows 客户端截图矩阵已完成，证据入口为 [output/art-ui-v01/qa/validation.json](../../output/art-ui-v01/qa/validation.json)，记录时间为 **2026-09-24 07:46:28 UTC**。正式归档 **59 张 PNG、59 份配套 JSON 和 1 份汇总 JSON**，共119文件、42,252,334字节（约40.30 MiB），没有复制运行日志。PNG均与汇总中的SHA-256一致，60份JSON均与最终验证源逐字节哈希一致。
- 本轮59次截图 **0 引擎错误**，布局根最小高度/关键按钮边界检查全过；119个关键按钮实例均在画布内，最高根最小高度1022、最大根最小宽度1176。**9组相同公开信息的A/B截图像素完全一致**：城市、现场、未知运输各100/125/150%。这只覆盖明确的前观察配对样例，不等于穷尽所有规则组合。
- 主任务及城市界面子任务已目视核对城市/经营状态、现场/空槽、实物、日结/终局、运输、设置与额外区域；150%设置与额外区域的最终布局也已复核。每图JSON记录画布、缩放、可见标签字符数/字号、按钮几何。Windows真实操作已完成新局、锁定取消/执行、点灯与空槽、日志、轮末、标题/继续、设置、撤离、日结与回查、退出，见独立[interactive.md](../../output/art-ui-v01/qa/interactive.md)。窗口生命周期调整后新进程复测stderr为空；更广玩法组合、真人审美及macOS仍未据此全部通过。
- 代码以下按文件和函数定位，避免正在调整 UI 的行号漂移。QA 参数创建隔离的未保存样例；额外借用、长文本等演示与实际游戏状态分开。

## P1–P5 交付组成

Windows x64 本地试玩 ZIP 已由源码提交 `afd9c0bf3043c984230993667e99baa5913f0344` 导出。包内自带 .NET 8.0.31；新目录解压后的198文件逐项校验一致。清空 DOTNET_ROOT / DOTNET_ROOT_X64 并从包外目录启动，标题及150%城市、现场、运输、设置五个场景均退出0、无运行错误，截图与源码版逐像素相同。包大小97,395,813字节，完整校验与结果见 [package-validation.json](../../output/art-ui-v01/qa/package-validation.json)。本轮交付本地ZIP与GitHub实现分支，没有创建新的GitHub Release。

| 包/资源 | 本次实现 | 证据与边界 |
| --- | --- | --- |
| P1 / VIS-CITY-01、VIS-NODE-01 | 城市背景、7 建筑切片、真实七节点七边、文字归属/营业/队伍/威胁、可选道路与封控标记 | [Main.City.cs](../../Scripts/Main.City.cs)、[CityPresentation.cs](../../Scripts/CityPresentation.cs)、[NetworkMap.cs](../../Scripts/NetworkMap.cs)。节点资料与标记为可编辑控件；三档截图/经营状态已留证并目视核对。 |
| P1 / VIS-PLAN-01 | 6 人岗位汇总、2 个事务卡槽、唯一工具、预算和固定底部提交区 | 同上；旧点、新建、预付与条件收入按实际模型表达，不冻结数值平衡。 |
| P2 / VIS-ROOM-01 | 1 张中性镜面房底图；同锚点事实异常叠层、真实照明文本、固定导航/退出距离 | [FieldScene.cs](../../Scripts/FieldScene.cs)、[Main.Field.cs](../../Scripts/Main.Field.cs)。未另造 A/B 暗示图；其它房间暂复用该首轮底图，不是完整场景库。 |
| P3 / VIS-CARD-01/02/03、VIS-HAND-01 | 调查、点灯、模仿 3 插画；共用卡片模板、五槽、空槽、参考/当前对象、独立借用区域排版 | [Main.Field.cs](../../Scripts/Main.Field.cs)。其它行动复用三张插画；有借用牌是明确隔离的 QA 排版样稿，不引入尚不存在的借用来源/玩法。 |
| P4 / VIS-ITEM-01/02、VIS-TOOL-01 | 座钟、灯、封装工具各 1 主体，合法状态说明、余量、唯一预留、补充生效日 | [ItemPresentation.cs](../../Core/ItemPresentation.cs)、[Main.Items.cs](../../Scripts/Main.Items.cs)、`RenderTools`。同一切片跨状态复用。 |
| P5 / VIS-PREVIEW-01、VIS-FEEDBACK-01 | 五段常驻预览、事实/候选/规程、触发/抵挡/心志文字、真实七阶段日结、静态与续玩反馈 | [Main.Field.cs](../../Scripts/Main.Field.cs)、[Main.Interface.cs](../../Scripts/Main.Interface.cs)、[CityEngine.cs](../../Core/CityEngine.cs)。三档长预览/终局有图；Windows正常轮界继续、日结回查已实操，更广奖励恢复组合仍待完成。 |

## AR-01–32 实现矩阵

| ID | 实现/状态 | 直接证据 | 限制及后续验收 |
| --- | --- | --- | --- |
| AR-01 | 候选已接入：现代都市夜景、旧旅馆木/玻璃/金属、专业档案式文字区 | [资产清单](assets.json)的 city/room/atlas；`BuildTheme`、`RenderHeader` | 仅候选方向；公司标识、员工面貌、最终品牌不冻结；AT-01 待四人评审。 |
| AR-02 | 候选已接入：约定色板、半写实素材、实底正文面板，字体基准 19 px | `Main.BuildTheme`、`Main.Field.FieldSurface`；3 张原图 | 暗场比例不是正文压暗硬指标；目标显示器实际对比、色弱/灰度阅读需 AT-11。 |
| AR-03 | 候选已接入：背景/遮罩在路线和文字后；装饰不接收鼠标；稳定边框 | `AddArtBackdrop`、`NetworkMap.ConfigureInternal`、`ArtAssets.Image` | 远景桥梁只是装饰；缩放后可辨认性仍需图片核对，不能把背景道路当第八条连接。 |
| AR-04 | 部分覆盖：同一背景以普通/增强星空氛围对照，未新增两套完整资产 | `CosmicIntensity`、`ShowInterfaceSettings`、`NetworkMap` 背景遮罩 | DA-01 现实/宇宙比例、悬浮感、恐怖强度仍未决定；强度开关不等于艺术方向评审通过。 |
| AR-05 | 候选已接入、核心已测：文字与形状分别标识归属/营业/队伍/威胁；未知不按隐藏真值着色 | `CityPresentation`、`MapPoint`；`ArtPresentationTests.ReadOnlyAndHiddenTruth`；QA灰度图和9组像素配对 | 暖灯不是安全信号。9组配对已一致；真人灰度语义辨识与更广状态组合不据此全部判定通过。 |
| AR-06 | 已实现、核心已测：7 节点/7 双向边来自模型；北/南线在医院汇合 | `Main.RenderCity` 使用 `g.Edges`；`M2CityTests.TopologyAndEvidence`；三档城市图 | 位置仅为排版，拓扑未重造；城市与状态截图已目视核对，实际道路点击/交互串联仍待导出包验证。 |
| AR-07 | 候选已接入：独立归属、建设/生效、营业缺口、队伍、选中、已知威胁 | `Ownership`、`Operation`、`OperatingConditions`、`TeamAt` | `city-states` 是状态样稿，包含测试构造；不能替代全部经营转换的交互证据。 |
| AR-08 | 候选已接入、核心已测：实线通路、缺口中断、虚线运输、公开箭头、封控条纹及道路详情 | `NetworkMap.DrawNetwork/DrawArrow`；`RenderCity` 仅用 `Forecast`；`CityTests` 运输预览配对；三档transport像素对照 | 不编造具体断路原因；封控明确为安排且实际生效依赖条件。未知运输三组客户端像素配对一致，不代表所有运输组合已穷尽。 |
| AR-09 | 候选已接入、核心已测：人力总/占/可用、5 类岗位、2 事务、共享工具与预算 | `RenderCityDispatch/RenderCityFooter`、`EmployeeRole`；M2 City/Art presentation 测试 | 准备占用是排期，锁定后保持；完整混合岗位状态读图尚需人工完成 AT-04。 |
| AR-10 | 候选已接入：固定人力/事务/预算栏，滚动地点资料，固定地点排版 | `RenderCity`、`RenderCityFooter`、`CityPresentation.Position`；三档城市图和边界记录 | 三档截图已留证，150%关键操作仍在画布内；实际长资料滚动/点击仍待导出包串联。 |
| AR-11 | 候选已接入：固定房间图、左门右镜，公开房间导航、出口距离、执行/撤离固定区 | `RenderField`、`FieldScene.Configure/_Draw`；manifest 源图锚点 | 图像裁切和异常叠层使用相同 cover 变换；房内插画不新增可拿物/可走门；完整多房素材不在本轮。 |
| AR-12 | 已实现、核心已测：事实/假说/规程分栏；基础房间与按钮不接收隐藏答案；实际观测后才分化 | `AddRule`、`FieldScene` 参数、`FieldEngine.ProjectAction`；核心配对测试及9组客户端像素配对 | A/B相同只要求发生不同观察之前。9组已覆盖当前前观察城市/现场/运输；其它音画/排序/弹窗组合仍不宣称穷尽。 |
| AR-13 | 候选已接入、核心已测：真实照明单列；规则反应、护符抵挡次数、最终心志与日志分别显示 | `RenderFieldFacts`、`FieldFeedbackSummary`、`FieldScene._Draw`；M2 Field/Ward clip 测试 | “未观察到”保留不等于安全；无隐藏完整因果先验。需要观察截图确认遮罩没有掩盖事实。 |
| AR-14 | 候选已接入、核心已测：经营日与轮次分列；行动/心志/护符/墨水/照明/异动/封门可查 | `RenderHeader`、`RenderField`、`FieldEngine.Preview`；只读序列化对照 | 查资料/更改显示不调用执行；菜单/长日志关闭再操作的客户端流程仍需复核。 |
| AR-15 | 候选已接入：3 插画共用名称/费用/目标/效果/耗尽位置，正式文字独立排版，可展开说明 | `RenderFieldHand`、`CardSummary`、`RenderFieldPreview` | 当前卡名均短；未来长名样稿与真实内容分开，不能据短名判定所有本地化通过。 |
| AR-16 | 已接入、部分覆盖：五槽固定；用第二/第四槽留空；独立借用区，执行/轮末在固定区 | `RenderFieldHand`；`M2FieldTests` 固定槽回归；`field-used/field-extra` QA | 有借用牌仅排版样稿，正常 M2 无借用来源；不把静态样稿计为借用玩法实现。 |
| AR-17 | 候选已接入、核心已测：参考动作与当前房间分别显示；疾走保留参考，不合资格说明 | `RenderField`、`CardAction`；Field/M2Field 关于模仿、基础封装/封装牌的测试 | 甲室→乙室→模仿的客户端完整操作证据仍待补齐；暂无真正多层内容。 |
| AR-18 | 部分覆盖、核心已测：牌库/弃牌/耗尽计数及模仿持续提示；取消入包仍支付动作的说明 | `RenderFieldHand/RenderFieldPreview`、`FieldEngine.Execute` | 本轮没有新增随机奖励选择界面或遗物栏；随机奖励选择全流程不能以计数展示冒充已验收。 |
| AR-19 | 候选已接入、核心已测：同一 ID/切片跨现场、背包、仓库、出售/交付；座钟无安装能力 | `ItemPresentation.ArtId/Describe`、`RenderFieldItem/ItemDossier`；ArtPresentation 物品测试 | 图集是 RGB 深底，非透明抠图；多份状态样稿不表示多件库存。 |
| AR-20 | 候选已接入、核心已测：工具容量/余量/预留/地点、生效日；灯未封装/安装/额度/停业 | `ItemPresentation`、`RenderTools`、`RenderFieldInventory`；M2 City/Save/ArtPresentation | 未封装灯公开报价可作为既有价格事实，不能呈现为可执行出售；支援额度从真实实例取值，拆装/读档不刷新。 |
| AR-21 | 已实现、核心已测：当前钱、已付/待付、固定/条件收入分开；出售仅登记，工程次日生效 | `CityEngine.GetBudget`、`RenderCityFooter`、`ItemPresentation`；28→25 与+4/+6/−2测试 | 不以图中暖窗或动画表示营业；潜在收入不作保证。数值为 M2 验证配置。 |
| AR-22 | 候选已接入、Windows截图已做：1920×1080画布、默认19px、100/125/150%和独立压力样稿 | [project.godot](../../project.godot)、`BuildTheme/LoadInterfaceSettings`、`SaveUiWitness`；59图与下文字符统计 | 窗口override是1440×810；最低1280×720为代码约束，非最终最低规格承诺。macOS及其它目标显示器未验证。 |
| AR-23 | 候选已接入：左状态、中现场、右资料+常驻预览、下手牌；长区滚动且执行在外 | `RenderField/RenderFieldPreview`、`Pane`、`ShowLongText` | 全文窗口是显式打开的模态阅读区，关闭后回原操作；可见区域/长日志需以最终截图和实际点击核对。 |
| AR-24 | 候选已接入：灰度开关、文字/符号多通道、静音和静态设置 | `AddAccessibilityOverlay`、`NetworkMap`、`RenderFieldFacts` | 尚无音频专属线索；灰度图片存在不等于用户可辨识评审通过，不宣称 WCAG 或最低字号认证。 |
| AR-25 | 已实现、核心已测：五段预览、未知竞争不报确定净损失，确认仅锁定/撤离/稀有/覆盖档/公开即败 | `RenderPreviewSections/ExecuteSelected`、`ConfirmDialog`、`PreviewTransport`；M2Field/Disturbance/City Tests | 长运输清单/确认层级客户端复核仍须最终记录；普通行动没有新增强制弹窗。 |
| AR-26 | 候选已接入、核心已测：现实现以静态反馈为主；显示只读、不延迟结算、不自行发收益 | `FieldScene`、`PerformFieldAction`；`ReadOnlyAndHiddenTruth` | 没有完整动画播放库，不能宣称已验证未实现的动画时序或跳过交互。 |
| AR-27 | 已实现、核心已测：真实SettleDay记录7阶段、原始事件和金额，正常日及终局可回查 | `CityEngine.SettleDay`、`RenderCitySettlement`、`RenderOutcome`；核心账例、三档图及普通日结回查实操 | 普通日结7阶段与只读页已实操，终局入口已目视；全部终局回查点击尚未覆盖。静态不重放收入；完整三城新城信息未实现。 |
| AR-28 | 候选已接入、核心已测：全静态默认、静音、最近成功检查点、退出回退提示、继续不重播 | `Main.Interface`、`Save/ExitMessage/RenderWelcome`；Save/M2Save/ArtPresentation | 当前无装饰动画，静态开关是预留策略；现有持久化配置 `profile-m2` v2，客户端恢复组合仍需复核。 |
| AR-29 | 已接入：3 源 PNG 与 15 ID；AtlasTexture 切片；文字/路线/反馈/异常框是控件或绘制层 | [ArtAssets.cs](../../Scripts/ArtAssets.cs)、`FieldScene`、`NetworkMap`、[assets.json](assets.json) | RGB 素材不假装透明；异常为代码同锚点叠层；没有 PSD/分层位图工程或人物素材库承诺。 |
| AR-30 | 文件记录完整、目标适配部分覆盖：真实尺寸/有效区/锚点/缩放/背景/alpha均登记 | [assets.json](assets.json)的 sources/assets/atlas；Godot PNG import | 两背景1536×1024，atlas1254×1254（并非请求的2048）；深底RGB无alpha边缘验收；显存/性能/macOS待复核。 |
| AR-31 | 已记录：三项完整原prompt、同一已看参考板、实际输出文件和无后处理说明 | [city](prompts/city-v01.md)、[room](prompts/room-v01.md)、[atlas](prompts/atlas-v01.md) | 工具未披露模型版本或seed，明确为空；没有编造。首轮风格一致性目视检查不替代团队选稿。 |
| AR-32 | 已记录、审批待定：15稳定ID、v0.1候选版、源/导出、尺寸、用途、状态、AR/AT与权利说明 | [assets.json](assets.json)、本矩阵、完整prompt记录 | 源图原样复制入Assets；生成缓存不属于正式交付。参考板权利、团队批准、DA项均保留待确认。 |

## AT-01–15 验收矩阵

| ID | 已有证据/可执行配置 | 本轮审计结论与尚缺内容 |
| --- | --- | --- |
| AT-01 | 城市/房间/atlas、标题及星空强度对照；统一候选色板与材质 | **待人工评审。** 没有四人意见、DA-01/02结论或视觉冻结。不能据AI生成成功判通过。 |
| AT-02 | WorldFactory真实七边；City/M2City拓扑测试；三档`city/city-states`截图及目视核对 | **核心与Windows样稿已核对。** 已看连接/状态图，背景装饰不计网络；实际道路点击等导出包交互仍待完成。 |
| AT-03 | `CityPresentation`独立状态、三档`city-states`、灰度图，城市子任务目视核对 | **状态稿已做，交互/真人辨识部分待定。** 样稿涵盖经营状态叠加；完整经营转换、混合缺口与真人灰度辨识未据此全部通过。 |
| AT-04 | 6人2事务、岗位汇总、唯一工具；M2 City/预留回归；三档城市与实物图 | **核心和初始调度样稿已做。** 混合研究/恢复、取消未消费/执行后消耗仍需导出包操作串联，不能只由初始截图代表。 |
| AT-05 | 核心公开信息配对；城市/现场/未知运输各100/125/150%，共9组PNG哈希相同 | **9组客户端前观察配对已通过。** 真实观察后分化是正确结果；更多规则/排序/弹窗组合未穷尽，当前无新增音频线索。 |
| AT-06 | 三档`field/field-lit/feedback`；固定锚点/真实照明/出口；实际点灯、日志、轮末与撤离 | **样稿及基本现场操作已做。** 点灯扣行动、日志不推进、轮末熄灯与入口撤离均有交互记录；其它房间/后果组合仍未穷尽。 |
| AT-07 | 三档第2/4空槽及extra样稿；真实点灯后第一槽空、其余稳定，轮末重填槽 | **样稿及普通使用/换轮已实操。** 额外牌仅排版QA、无正常M2借用来源；未用/已用模仿耗尽的完整客户端组合仍待实操。 |
| AT-08 | Field/M2Field覆盖参考、疾走、封装、真实库存、耗尽与新出勤 | **核心已测，客户端流程未整体完成。** 甲室→乙室→模仿、回访/换轮/重开出勤需操作证据；当前房间文本不代表完整流程验收。 |
| AT-09 | 三档实物页、实物状态对照图及目视核对；M2 City/Save/ArtPresentation | **核心与实物样稿已做。** 灯具发现→携带→补封装→安装→停业→恢复仍需导出包串联；同实例/额度恢复核心已覆盖。 |
| AT-10 | 三档搜查299字符/护符211/运输92，独立压力576；实际锁定取消SHA不变、撤离确认成功 | **长预览及主要确认入口已验证。** 运输三档配对相同；已知即败/唯一稀有等全部分支仍待实操，压力副本不冒充真实内容。 |
| AT-11 | 59图/配套JSON；1920×1080、三档缩放、Noto字体、灰度/压力文案；0边界失败 | **Windows截图/几何与关键样稿目视已完成。** 最高根最小高度1022，119个关键按钮实例在画布内；真实点击/滚动、其它显示器及macOS未由此认证。 |
| AT-12 | 928回归与三档日结/终局图；实际16→15预付、日结13且7阶段，右侧日结只读可开 | **核心、样稿与普通日结回查已做。** 全部终局回查入口实操仍待；无完整动画/跳过版本，费用不足/0余额核心测试不替代全部状态图。 |
| AT-13 | 静态/静音150%实际操作；Round2继续后相同Payload SHA、资金15，设置应用/撤离/日结/退出正常 | **普通轮界恢复和设置流程已实操。** 有/无较晚奖励检查点的更广组合仍需操作记录，不声称全平台续玩通过。 |
| AT-14 | 3PNG、15ID、尺寸/alpha/锚点/hash/prompts、字体OFL；119件QA证据原样归档 | **文件与关键样稿检查已做。** 无生成乱码当文字；RGB暗底不假装透明；参考板权利审阅和最终团队选稿仍待确认。 |
| AT-15 | 47项矩阵、P1–P5、assets清单、候选状态；59图/9组配对及独立interactive记录 | **文档、Windows截图和基础交互已记录。** 更广组合待补；macOS、真人审美冻结、三城、人物/3D/动画/发行美术未宣称完成。 |

## 最终样稿与文本证据

59张截图覆盖16种配置：`city`、`city-states`、`field`、`field-used`、`field-lit`、`field-extra`、`feedback`、`items`、`settlement`、`preview-long`、`preview-ward`、`transport`、`outcome`、`settings`、`title`、`item-states`。其中前14种各有100/125/150%图；标题/实物状态各1张；城市/现场/运输另有B真值三档、灰度和增强星空对照。所有图及配套JSON详见[证据目录](../../output/art-ui-v01/qa)。

| 文本类型 | 本次实际字符数与字体 | 依据与口径 |
| --- | --- | --- |
| 最长真实卡名 | 2字符，10种卡名均相同长度 | [CardCatalog.cs](../../Core/CardCatalog.cs)全部10项，不用虚构长名代表当前真实内容。卡面名称基准20px。 |
| 最长真实卡牌说明 | “模仿”29字符；其次“搜查”“疾走”均26字符 | 当前CardCatalog.Description，含标点/空格；展开说明正文18px，字数统计不包括另加的UI标题/耗尽说明。 |
| 正常搜查真实预览 | 299字符；正文18px、段标题14px | [field 100%记录](../../output/art-ui-v01/qa/art-field-a-1.00-color.json)。按五段原文含换行重建，去除UI增加的“01 /”等序号；不确定性单段143字符。 |
| 已知护符后果真实预览 | 211字符；正文18px、段标题14px | [preview-ward 100%记录](../../output/art-ui-v01/qa/art-preview-ward-a-1.00-color.json)。明确触发、防护消耗和已确认条件下的结果。 |
| 未知运输确认全文 | 92字符，基准19px | [transport 100%记录](../../output/art-ui-v01/qa/art-transport-a-1.00-color.json)。包含免费撤离、归队/日结边界与未知线路风险。 |
| 未来加长压力副本 | 单独576字符，12次重复，正文18px；另有18字符的压力标识标题 | [preview-long 100%记录](../../output/art-ui-v01/qa/art-preview-long-a-1.00-color.json)。原299字符真实预览仍保留；576字符是额外排版压力，不是新增规则或真实最长内容。 |

字符数采用C#/.NET字符串Length口径，计入标点、空格和换行。当前文本不含需要特殊代理对计数的字符；这是文本长度，不是显示行数。三档字号和逐控件实际文本在各图JSON中保留。此组样例不是所有运行状态可能产生的全局最长预览证明；未来卡名加长、其它规则组合和本地化仍需继续检查。

最终界面保持：终局完整时间线/资金/行动记录只读入口；建筑完整主体缩放；可见且明确标识的压力副本；独立借用区域样稿；未知运输真实确认；按真实ReservedBy显示唯一预留。额外借用只验证排版，没有新增正常M2借用机制。研究入口已用于仓库中已封装、非必需的Lamp/Anomaly，确认明确只消耗并登记，不承诺额外现金/能力；引擎继续拦截已预留/出售等冲突。研究交互不以59图或本次普通日程实操冒充已覆盖。

## 隐藏信息边界审计

- `FieldScene` 接收房间名称、真实照明、公开敌影、已记录反应等公开输入，不接收 `MirrorVariantB`、`CorrectCandidate`、未知物品或规则真值。房间底图固定为 `room`。
- 局部镜面异常叠层只基于已保存的真实反噬观察。相同公开状态的A/B不能提前换图；实际观测不同之后不强求画面相同。
- `AddRule`分别处理公开告示、可靠历史、实际观察、候选、已确认答案。候选排序沿公开列表，禁用依实际已排除项/提交配额/阶段；不使用隐藏正确下标作为禁用条件。
- `FieldEngine.ProjectAction`只把已确认规则作为精确后果；未知项存在时保留不确定性，不能假定护符分配或净损失。已知立即失败确认仅来自公开确定条件。
- 城市箭头来自已公开 `Forecast`；运输预览对未确认线路只给不确定性，不提前列隐藏受侵节点。当前未知运输示例使用明确标识的 Legacy 内容，不能冒充 M2 新增线路规则。
- 只读展示已比较序列化状态/PRNG，保存的时间线是结算结果快照，不是读取时重新执行业务。9组客户端整图配对已覆盖相应画面/控件文字与运输确认；普通继续游戏的Payload SHA也已实测不变。未覆盖组合不扩张为全状态证明。

## 资产、提示词与权利记录检查

| 项 | 实际记录 | 结论/限制 |
| --- | --- | --- |
| 原图与导出 | `Assets/Art/city-night-v01.png`、`room-mirror-neutral-v01.png`、`subjects-atlas-v01.png` | 原PNG直接复制；无外部脚本重绘/裁切。所有正式数字、文字、路线与状态保持可编辑。 |
| 画布 | 背景各1536×1024，atlas1254×1254，4×4格，每格313.5 | manifest登记工具实际输出，未把请求2048写作真实输出。 |
| Alpha/背景 | 三图均RGB、无Alpha，适合深底；异常/光线/状态由控件表达 | 无透明PNG不等于丢失Alpha；本批从未声明它们是抠图。浅底透明边缘与跨平台材质适配未执行。 |
| 提示词/参考 | `docs/art/prompts/`三份完整原prompt；统一项目样板及首轮编辑记录 | 没有缺失的修订过程；实际没有第二轮生成。参考板已看，版权/授权最终确认仍保留。 |
| 模型/种子 | 内置 `image_gen.imagegen`，工具未披露模型名/版本/seed | 清单明确null/未披露，不编造。 |
| 版本/使用状态 | `0.1.0-candidate.1`，15稳定ID，各项用途/状态/AR/AT/源/导出/hash | 已选用于候选实现，未团队批准；其它卡复用首轮图，不冒充完整图库。 |
| 中文字体 | `Assets/Fonts/NotoSansCJKsc-Regular.otf`；[Licenses/NotoSansCJK-OFL.txt](../../Licenses/NotoSansCJK-OFL.txt) | 实机显式加载该字体，随仓库保留OFL1.1文本；这是许可记录存在性核对，不是法律清权意见或最终字体批准。 |
| 外部权利 | 三图为本轮AI新生成，未增加外部图库/商标图；项目参考板见manifest | 未声称独占版权、第三方许可已自动取得或团队已签字。参考板来源/使用权仍由维护者确认。 |

## 明确保留的后续工作

- DA-01/02：四人对候选风格、恐怖强度、宇宙比例、材质与装饰密度作明确选择。
- DA-03/04：在真实目标显示环境复核字重、字号、缩放、锚点、贴图导入和预算；macOS 尚未执行。
- DA-05：目前静态为主，没有完整装饰动效/跳过方案的体验评审；新增动效时必须重做对应时序和低动态验收。
- Windows59图、9组像素配对及基本实际交互已记录；后续补齐灯具/研究/模仿/较晚奖励恢复等组合流程。首次真人推理与玩法取舍观察仍需真人试玩。
- 完整三城生成、全卡池/员工人物/3D/动画与发行美术不属于此次样稿实现；本文件不以单城证据替代它们。
