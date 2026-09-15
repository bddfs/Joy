# 异常、守则与敌影

[Wiki 首页](README.md) · [卡牌](cards.md) · [实物与工具](items.md) · [地点](locations.md) · [机制](mechanics.md) · [版本与来源](sources.md)

本页包含完整规则答案与试探方法。**当前 M2 每局实际配置 3 条守则；镜中住客从 A、B 中固定选择一种。** 普通敌影、异动阈值和封门是额外的公开机制，不计作这 3 条隐藏守则。开启传播压力不会给 M2 追加历史南线运输守则。

“当前实现”均指开发提交 [`9cbe156`](https://github.com/bddfs/Joy/tree/9cbe156779aa38dbce0b798b5db0143df5282846)，不是已合并或已发行声明；“当前需求”指 main 提交 [`6933076`](https://github.com/bddfs/Joy/tree/693307638a7d9521b8ee185628d8d685b66e15b2)。本页通过固定提交的源码与文档核对，没有运行游戏或重做测试。策划案中的“未实现”是该文档写作时的状态，后续实现应单独看开发提交；实现中的差异也不自动改写需求。

## 收录范围

| 状态 | 内容 | 如何理解 |
| --- | --- | --- |
| M2 实际配置 | 镜中住客 A/B、病历室规程、源头回收规程 | 每局 3 个规则实例，展开 A/B 后共 4 个真值条目 |
| M2 公开危险 | 1 类普通敌影、异动阈值袭击、封门；另可开启源头传播 | 不用猜候选；照公开时间、区域与资源处理 |
| 历史场景 | 暗处搜查、门内应答、暗室病历、值班禁眠、红箱外溢、南线运输守则 | `CreateLegacy` 共 6 个实例，不能照搬到 M2 新局 |
| 已定义、未投放 | 暗处移动、灯光结束；全城范围 | 引擎能处理相应事件/范围，但两个工厂都未生成对应具体守则；没有正式名称、线索或配置伤害 |
| 规划/示例 | 线路与全城递进、约 18 条完整规则池、稀有揭晓扩展 | 内容清单未冻结；不得把预算补写成 18 条“现有规则” |

工厂、事件枚举和规划预算的依据：[M2 配置](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/WorldFactory.cs#L36-L52)、[Legacy 配置](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/WorldFactory.cs#L114-L122)、[完整触发类型](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/Models.cs#L12-L14)、[当前策划内容预算](https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/design/异常回收公司详细策划案_v0.2.md#L443-L445)。

## 读守则前先掌握的边界

可靠线索和实际日志可信；原始告示与玩家假说不是同一层级。获得线索不等于已确认规则，其他地点的同一家族也不会自动沿用答案。当前现场先取得相关线索或观察，再提交候选；全队所有规则共用每轮一次提交，不额外耗行动。首次正确现场确认恢复 1 心志并得 1 零钱，错误加 2 异动、立即检查阈值。城市确认使用日终已归档证据，每条每天一次，不发现场奖惩；城市当天已判断的同一条不能转到现场再交。已确认规则不能重复领奖，错误候选跨模式保留且不能再交。[需求：证据与确认](https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/requirements/Joy_需求规格说明书_v0.3.md#L298-L308)；[现场确认实现](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L259-L287)；[城市确认实现](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L208-L228)。

一次行动内，同一规则实例最多触发一次；疾走逐格经过两个房间也遵守此上限，不同实例仍分别检查。结算按全城、线路、地方及同范围固定顺序进行。每枚护符取消一项允许抵挡的负面规则后果；护符被用掉，说明事件已经发生，不能据“心志没降”判定没有反噬。心志一旦归零立即失败，不能等后续恢复补救。[行动边界需求](https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/requirements/Joy_需求规格说明书_v0.3.md#L306-L310)；[排序](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L66-L68)；[实例去重与护符](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L522-L556)。

**模仿绕过的是原行为身份，实际物品与结果仍存在。** 模仿搜查不算正常搜查，也不增加镜面 B 的次数；它仍扣当前位置的真实库存，并产生获得、携带等结果。徒手搜查算正常搜查，但不是牌，不创建模仿参考；疾走保留已有参考且不创建新参考。成功模仿每出勤耗尽，普通搜查与模仿没有两份库存。完整白名单、参考覆盖和共享次数见[卡牌](cards.md)。[最新模仿需求](https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/requirements/Joy_需求规格说明书_v0.3.md#L320-L326)；[正常搜查与入包事件](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L128-L174)。

## 镜中住客：M2 主推理家族

| 项目 | 当前内容 |
| --- | --- |
| 地点与范围 | 暮灯旅馆；仅“镜面房”和“未拼接的镜面侧房”两个房间。入口与安全值班室不在该规则作用范围 |
| 实例关系 | 两房共享同一条规则、真值和确认记录；B 的正常搜查次数仍按各房分别累计 |
| 变体 | A：未照明正常搜查反噬；B：同房本次出勤第二次及以后正常搜查反噬 |
| 后果 | 每次触发损失 2 心志；允许用 1 护符取消该项后果 |
| 计数 | 搜查牌、徒手搜查增加正常次数；模仿不增加。离房重进不重置；新出勤重置行为次数，容器、实物、奖励不刷新 |
| 目标 | 主房第一柜给一次寻物记录，定位第二容器；第二容器中的合同座钟还须搜查或模仿。侧房有唯一侧房灯 |
| 变体固定 | 建局时选择，之后不随打法改变。工厂未显式指定时，以种子奇偶选择：奇数 A、偶数 B；这是此验证工厂的规律，不是完整随机生成规则 |

[房间、证据和真值配置](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/WorldFactory.cs#L36-L76)；[默认变体选择](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/WorldFactory.cs#L10-L14)；[最新内容契约](https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/requirements/Joy_需求规格说明书_v0.3.md#L773-L788)。

### 可靠旧录像：能排除什么

旧录像属于**同一异常实例的上一趟出勤**：调查员在黑暗中进入镜面房，没有观察到反噬；随后自称在做第二次搜查，发生反噬，第一次搜查录像缺失。它能排除第三候选“黑暗进入镜面房就会受伤”，同时仍支持 A 与 B。不能从这段旧账推定“第一次搜查必定安全”，也不能把录像的两次搜查加到本次计数。M2 手操开始时会获得这份可靠历史记录。[当前需求](https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/requirements/Joy_需求规格说明书_v0.3.md#L781-L788)；[进入现场获得记录](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L54-L58)。

### A：未照明的镜面房正常搜查会反噬

发生地点是两个镜面房，条件看**搜查前是否照明**。第一次正常搜查也会触发，之后每次满足条件仍触发；每项损 2 心志或消耗 1 护符。本次首次在暗处搜查档案柜，记录“正常次数 0→1、发生反噬”；即使护符使最终心志不变，这个观察也支持 A、排除 B。[触发实现](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L528-L554)；[对照观察记录](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L630-L646)。

已确认后，可以先点灯、在本轮正常搜查；也可以用已建立的搜查参考进行模仿，省掉一次照明行动。点灯只持续当前轮，结束轮次后再次搜查要重新看照明。移动进入暗处本身不会触发这条搜查规则。主房档案柜与后续合同物是两次真实搜索，定位容器并不授予免费拾取合同物的资格。[策划应对](https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/design/异常回收公司详细策划案_v0.2.md#L248-L265)；[容器/拾取约束](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L414-L423)。

### B：本次出勤同镜面房第二次及以后正常搜查会反噬

触发从该房正常搜查次数达到 2 开始，与灯光无关。主镜面房和侧房分别计数：主房已经搜查两次，不会使侧房的第一次正常搜查变成第二次；但同房离开重进仍保留计数。新出勤重置正常行为次数，不补回搜过的容器或拿走的物品。[最新次数定义](https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/design/异常回收公司详细策划案_v0.2.md#L229-L242)；[按房间记录与触发](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L154-L166)；[第二次阈值](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L531-L532)。

首次暗处正常搜查没有反噬，支持 B、排除 A。确认后点灯不能免除第二次正常搜查的损失；可用模仿获取仍存的第二容器目标，或者准备护符/心志承担已知代价。模仿不增加也不清空正常次数：已有一次正常搜查后，即使中间使用模仿，下一次合法正常搜查仍按第二次结算。必须仍有真实可搜查目标，不能在两柜已空时靠重复搜查制造次数。仅凭模仿没受伤无法区分 A/B。[试探与利用需求](https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/requirements/Joy_需求规格说明书_v0.3.md#L779-L788)；[模仿身份实现](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L156-L161)；[无库存拒绝](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L414-L417)。

### 现场试探和对照委派

| 办法 | 观察、真实消耗与限制 |
| --- | --- |
| 现场首次暗处正常搜查 | 搜查牌 1 行动或徒手 2 行动；消耗第一柜的一次寻物记录库存。A 反噬、B 不反噬；初始护符可挡住 A 的 2 点。徒手能试探，但不会建立模仿参考 |
| 先照明或直接模仿 | 都是合法策略；本身不能完成“首次暗处正常搜查”这一 A/B 对照。可以先获取成果或撤离，之后再补足判断 |
| 对照调查：原档案柜 | 先有已归档历史，且尚无本实例可区分观察。独立出勤暗处正常次数 0→1，消耗尚有库存的原柜，只定位第二容器，不领取合同物 |
| 对照调查：备用柜 | 原柜已用时，使用事先公开登记的一份备用样本；不是刷新原柜，样本用后耗尽。已有可区分观察后不再受理同实例对照 |
| 委派代价与产出 | 2 人、1 事务、基础出勤费 1；使用模板公开的 1 护符，不配发/消耗便携隔离工具。A 返回反噬和护符 1→0、心志 12→12；B 返回未反噬、护符 1→1、心志 12→12。日终归档，次日由玩家判断，不代选答案 |

[现场内容及费用](https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/design/异常回收公司详细策划案_v0.2.md#L246-L269)；[对照受理条件](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L557-L566)；[有限样本与实际返回记录](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L628-L647)；[人数和费用](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L835-L838)。

## 病历室规程：M2 医院教学规则

| 项目 | 内容 |
| --- | --- |
| 地点与范围 | 白榆医院现场；当前没有限定单一房间的规则掩码，凡该现场实际能正常搜查的位置均检查 |
| 真值 | 不要在黑暗中搜查：搜查前未照明，且实际产生正常搜查行为时触发 |
| 后果和计数 | 每行动最多一次，损 2 心志；1 护符可挡。没有“仅第二次”或“每出勤一次”的限制 |
| 可靠线索 | 值班记录指出亮灯后搜查、封装样本正常完成，摸黑搜查出现惊扰 |
| 应对 | 同一轮先照明再正常搜查，或用合法搜查参考模仿；不需要为普通移动套用搜查惩罚 |
| 独立收容条件 | 回声样本封装时仍要求本轮照明，并消耗便携隔离工具 1 次。用模仿避开搜查行为，不会免除该公开封装条件 |

候选另外两项“不要点灯”“不要调查”是错误假说，不是额外医院守则。医院一次允许取得的可靠调查记录已足以支持该固定实例判断；它和镜面 A 虽都有黑暗搜查条件，确认记录仍是各自独立的实例。[规则与物品配置](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/WorldFactory.cs#L49-L71)；[默认后果](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/WorldFactory.cs#L149-L156)；[现场范围与触发](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L522-L540)；[封装验证](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L429-L435)；[当前需求的医院配置](https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/requirements/Joy_需求规格说明书_v0.3.md#L798-L802)。

## 源头回收规程：M2 携带结果规则

| 项目 | 内容 |
| --- | --- |
| 地点与范围 | 红箱源头的整个现场；不是所有城市地点都挂着这条源头实例 |
| 真值 | 当获得/移动等步骤产生携带事件，背包仍有需要封装且未封装的物品时外溢 |
| 后果和计数 | 每行动同实例最多一次，损 2 心志或消耗 1 护符；取得后继续带着移动可在新的行动再次触发，疾走同一次行动只触发一次 |
| 可靠线索 | 回收记录记载未封装红箱入包发生外溢，用登记的源头专用箱收容后外溢停止 |
| 公开收容条件 | 先调查归档、确认源头规程，才能配发源头专用箱；只收容该源头合同物，不能用普通隔离工具替代，专用箱随源头交付 |
| 应对 | 预留护符/心志承担当次入包结果，及时用专用箱封装再移动；也可拒绝入包或放回。后一步封装不会撤销已经发生的入包反噬 |
| 模仿关联 | 模仿搜查仍取得并携带真实红箱，因此不能绕过这条结果规则。封装、物品身份、运输与交付要求都继续有效 |

规则答案文字为“异常物入包后应尽快封装”，实际代码条件更精确：检查背包中 `RequiresSealing` 的未封装实物，不以物品名称是否含“红”判断。普通待机、查看日志或结束回合不会凭空制造一次携带事件。拒绝入包不触发这次获得/携带，稍后实际拾取才产生携带结果。[规则与专用箱配置](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/WorldFactory.cs#L50-L80)；[取得与移动事件](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L140-L180)；[实际携带触发条件](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L534-L554)；[配发门槛](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L531)；[随箱交付](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L724-L739)。

## 普通敌影、异动与封门

这些危险有公开先兆；规则反噬不自动变成可以用驱离清除的敌影。

### 普通敌影

当前只有 **1 类未单独命名的普通敌影**，没有血量、元素或敌种变体。M2 的旅馆、医院、源头手操现场都共用出现条件：进入第 4、8、12 轮时，若全场没有活跃敌影且队伍不在入口，就在队伍当前房间生成一个。出现轮只预告，最早下一轮末攻击该房间；每次攻击伤害 2，1 护符可挡。它不在移动步骤追击，不攻击其他房间，出现当轮也不会发动攻击。[生成及意图](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L346-L375)；[唯一敌影数据](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/Models.cs#L272-L278)。

可靠信息就是出现日志、房间和预告轮次。可以在攻击前离开该房间、在当地使用驱离，或预留护符；敌影留在原房间，之后返回仍要看轮末风险。驱离必须有当前房间内明确可驱离的目标；当前生成的普通敌影都允许驱离。模仿不提供额外驱离能力，也不免疫敌影；正常/模仿护身得到的护符共同用于抵挡。第 12 轮生成的敌影虽然预告最早第 13 轮攻击，当前 12 轮封门仍会先终止本次现场。[驱离执行与验证](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L242-L250)；[合法目标](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L458-L460)；[需求敌影边界](https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/requirements/Joy_需求规格说明书_v0.3.md#L276-L278)。

### 异动阈值袭击

所有手操现场适用。异动通常每轮末增加 1；错误候选等来源也可增加。达到 **8** 后立即发生不可用护符抵挡的 3 心志袭击，但**设计要求与当前实现对袭击后的异动值不同**：

| 口径 | 袭击后的异动 | 例子：异动 7，错误候选增加 2 |
| --- | --- | --- |
| 当前设计基线 | 策划 v0.2 明写“回落到 4”；最新需求 P-FIELD 简写“回落 4”，沿用这一参数基线 | 先到 9，损 3 心志后设为 4 |
| 开发提交实际实现 | `AddDisturbance` 每次从当前异动**减去 4**，不是直接设为 4；若减完仍 ≥8 且队伍存活，继续循环袭击 | 先到 9，损 3 心志后为 **5** |

当增加后恰好为 8，两种口径都得到 4；超过 8 时不能混用。表内实现数值来自静态代码核对，是尚需处理的设计/实现差异，不是本页替设计批准的新规则。[策划的明确重置值](https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/design/异常回收公司详细策划案_v0.2.md#L176)；[最新需求参数](https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/requirements/Joy_需求规格说明书_v0.3.md#L476)；[实际减法与循环](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L559-L574)。

**护符不能抵挡，驱离不能清除阈值。** 点灯将异动减 1、最低 0；不要把下一轮能休息或点灯当作当前归零后的补救。模仿只影响原行为身份；由错误判断或公开轮末增长到达阈值，照样袭击。[点灯](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L221-L233)；[轮末顺序](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L346-L363)。

### 轮限封门

当前每次手操现场的轮限是 12，按**敌影→灯光结束→异动增长及即时袭击→封门**处理第 12 轮末。结束末轮会失败，包括已经站在入口却仍点击结束回合；正确做法是存活时先返回入口，免费撤离。护符、驱离与模仿都不能取消封门。公开轮数和轮末预览就是可靠先兆。[参数](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/Models.cs#L240-L246)；[撤离](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L290-L299)；[末轮预览与实际封门](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L335-L363)。

心志归零或封门失败后，M2 保留记录证据，携带实物连同带入装备按唯一位置遗落当前房间，未撤出零钱丢失，已用次数不返还；员工恢复一经营日。重返可以取回已发现遗落物，不刷新库存或确认奖励。物品细节见[实物与工具](items.md)，出勤结算见[机制](mechanics.md)。[失败实现](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L577-L584)；[恢复处理](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L290-L297)。

## 源头传播：默认关闭的独立压力模式

| 模式维度 | 当前配置 |
| --- | --- |
| 无压力 M2 | `pressure=false`，没有预告传播、交通附加费或强制抢修；轮内敌影、异动、封门与三条现场守则仍存在 |
| 压力 M2 | `pressure=true`，保持原有三条现场守则；日终进入偶数日准备阶段时，如源头未交付，生成当晚 1 个传播预告 |
| 当前 M2 目标 | 工厂第一条连接源头的边是“白榆医院↔红箱源头”，因此预告从源头指向医院；通常会在第 2、4 日看到，前提是远征仍继续且来源未回收 |
| 镜面与报价 | 隐藏 A/B、灯具公开报价 6/2、压力开关是独立维度；经营路线 A/B 也不是镜面真值 A/B |

[建局选项与默认账例](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/WorldFactory.cs#L10-L34)；[工厂声明](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/WorldFactory.cs#L83)；[预告生成](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L788-L796)；[实现记录对压力的限定](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/docs/implementation-m2.md#L11-L16)。

传播依靠公开箭头和当晚预告说明判断，不能用护符抵挡或现场驱离处理。先完成归队运输、归档交付及工程，再以共同快照判断所有封控和取消，未被阻止的结果同时落实，当晚不连锁。来源在当晚交付前完成回收，可以取消尚未落实的源头箭头。有效服务点可选择一条相邻边封控，但必须在快照时满足建成生效、驻站、连通、未入侵、维护正常，以及适用规程已确认归档等资格。[封控设置](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L197-L205)；[营业资格](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L408-L410)；[日结顺序](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L305-L339)；[共同快照落实](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L760-L785)。

入侵使目标失去有效营业资格，设施和旧规程仍在；维护清除入侵而不重新支付建设费，抢修恢复道路而不解除线路规则。M2 当前一个特别要区分的**实现文案差异**是：预告泛称“入侵并切断相邻通路”，实际 `ApplySpread` 只在目标为枢纽时切断该枢纽所有相邻边；医院不是枢纽，因此当前 M2 此分支只标记医院入侵，不切断医院道路。这不是需求批准的“医院免断路”设计，也不能据此认定压力模式已经完整验收。[预告文字](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L795-L796)；[实际断路分支](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L777-L783)；[恢复处理](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L669-L680)。

## 运输条件：先区分收容与线路规则

M2 当前所有路线都没有绑定运输守则；未封装侧房灯允许按已有物理与任务条件带回总部暂存。灯未封装不能直接出售或安装，之后补封装仍要消耗真实工具和工程资源。**开启传播压力也不会自动给灯追加秘密运输禁令。** 回声样本的照明封装、源头专用箱用途、背包容量等是独立公开条件，详见[实物与工具](items.md)。[M2 全部边与规则](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/WorldFactory.cs#L29-L52)；[未封装暂存需求](https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/requirements/Joy_需求规格说明书_v0.3.md#L346-L348)；[实现记录](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/docs/implementation-m2.md#L22-L28)。

线路运输规则当前唯一具体样例是下文的 **Legacy 南线运输守则**。引擎在归队返程逐边处理：如果经过绑定的 `TransportUnsealed` 规则，且清单有未封装异常物、灯具或明确需要封装的其他物品，就在该步**到达节点**登记入侵。同一步有多件符合物品并不会按件额外扣心志；实际结果是节点入侵及来源记录。手操带回清单与标准回收都调用运输结算，标准回收先按其规程完成封装。[真实返程](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L272-L278)；[委派回收](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L649-L660)；[运输条件与后果](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L742-L758)。

最新需求允许在满足物理和任务条件时承担未知运输风险，不要求手操先猜完全部未知规则；标准回收仍要求必需规程、工具、收容和运输条件齐全。预览只给已确认的后果与到达节点；未知部分不能通过高亮、精确损失、按钮可用性或提示差异泄露。“暂无已知运输后果”不等于安全。实际运输仍按全部适用规则结算，交付源头不会撤销已经发生的运输入侵。[最新运输需求](https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/requirements/Joy_需求规格说明书_v0.3.md#L240-L242)；[当前预览实现](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L412-L430)；[标准回收门槛](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L568-L579)。

模仿无法免除运输：它已经让真实物品进入背包。封装也不改写物品唯一身份；未来若有按颜色、身份或营业时序触发的规则，不能直接认定“封装全免”。当前实现的运输判断只检查物品种类/封装状态，没有红色身份触发分支，不应把历史的“红色物品”说明冒充 M2 已投放条件。[身份与模仿需求](https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/requirements/Joy_需求规格说明书_v0.3.md#L322-L338)；[当前实物字段](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/Models.cs#L186-L209)；[当前运输谓词](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L757-L758)。

## Legacy 历史守则全表

下表完整列出同一开发提交中 `CreateLegacy` 的 6 个实例。它们用于解释旧记录和历史场景，**不会因同名地点出现在 M2 就一起投放**。工厂没有给这些地方规则限制 `RoomIds`，因此范围是相应整个现场；动作还须有合法房门、容器或库存。全部默认允许护符抵挡负面现场后果；每行动同一实例最多一次。运输入侵不使用现场护符或默认心志伤害。[历史工厂](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/WorldFactory.cs#L87-L138)；[共同后果参数](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/WorldFactory.cs#L149-L156)。

| 守则 / 历史地点 | 触发、后果与周期 | 可靠线索与应对 | 模仿关联 / M2 对照 |
| --- | --- | --- | --- |
| **暗处搜查** · 暮灯旅馆 | 黑暗中正常搜查，损 2 心志或用 1 护符抵挡；不限制首次，每行动最多一次 | 值班记录比较亮灯整理无人受伤、熄灯搜查惊扰。先点灯再搜查 | 模仿搜查不产生原行为；M2 被镜中住客 A/B 替换，不与镜面家族叠加。[定义](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/WorldFactory.cs#L116) |
| **门内应答** · 暮灯旅馆 | 主动敲门行为损 2 心志或用 1 护符抵挡；无“第三下才触发”的代码计数，告示里的第三次敲击不能替代真值 | 可靠笔记比较不敲门者无事、主动敲门者受到回应。不敲门，或用合法办法取得尚有库存 | 模仿敲门不产生原敲门行为，仍要求允许应答的房门与真实库存。M2 没有此条。[定义](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/WorldFactory.cs#L117) |
| **暗室病历** · 白榆医院 | 黑暗中正常搜查，损 2 心志或用 1 护符抵挡；每行动最多一次 | 交班单说明亮灯后取病历与摸黑取物的差别。先照明、再搜查 | 模仿搜查绕过行为。M2 同一内部 ID 改为“病历室规程”，并增加样本公开照明封装条件；不能用旧名称判断新版全部收容要求。[定义](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/WorldFactory.cs#L118) |
| **值班禁眠** · 白榆医院 | 正常休息产生异动 +2；规则本身不扣心志。允许 1 护符取消该项 +2；未抵挡时立即检查异动阈值 | 观察记录比较坐下休息异动增加、走动没有该反应。避免正常休息或承担明确异动代价 | 普通休息先恢复 2 心志，再检查行为后果；模仿休息没有原休息行为，但与普通休息共用每房 2 次，不能刷第二套额度。M2 没有此条。[定义](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/WorldFactory.cs#L119) |
| **红箱外溢** · 红箱源头 | 获得或移动产生携带事件，背包有需要封装的未封装实物，损 2 心志或用 1 护符抵挡；后续新行动可再次触发 | 可靠回收日志记载未封装入包外溢、封装后停止。封装或暂不带走 | 模仿仍有携带结果。M2 改称“源头回收规程”并采用独立专用箱；Legacy 工厂使用普通封装工具体系。[定义](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/WorldFactory.cs#L120) |
| **南线运输守则** · 白榆医院↔南线中继 | 经过绑定边时，运有未封装异常物/灯具/需封装物，在当前行进方向的到达节点入侵；逐次实际运输检查，无护符抵挡，无直接 2 心志扣除 | 可靠运输日志比较未封装经南线到达节点留下侵入、封装运输没有记录。封装、换合法路线或不带走 | 模仿取得的物品一样检查。双向边意味着医院→中继影响中继，反向影响医院；不是只按“南线”名称一律影响总部。M2 未投放此条。[规则](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/WorldFactory.cs#L121)、[绑定边](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/WorldFactory.cs#L107-L112) |

表内行为豁免和次数的共同实现：[原行为身份](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L154-L174)、[休息先恢复](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L235-L240)、[共享次数与合法目标](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L414-L460)、[触发与阈值](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L522-L567)、[运输到达节点](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L742-L758)。

Legacy 默认开启传播，源头的第一条相邻边连接北街枢纽，所以其偶数日预告目标是北街枢纽；若入侵落实，该枢纽所有相邻通路断开。这是旧地图骨架导致的目标差异，不是 M2 压力会随机抽到北街枢纽。[历史开关与边序](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/WorldFactory.cs#L87-L112)；[固定首边选择](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L788-L796)。

## 引擎定义与尚未投放的内容

完整枚举共有 8 种触发类型。这里只把它们作为能力索引，**类型存在不代表有一条可遇见的具体守则**。

| 触发类型 | 检查事件 | 实际投放状态 |
| --- | --- | --- |
| `MirrorSearch` | 镜面房正常搜查，按 A/B 真值 | M2 镜中住客 |
| `SearchInDark` | 正常搜查前未照明 | M2 医院；Legacy 旅馆及医院 |
| `CarryUnsealed` | 携带事件后的背包仍有需封装且未封装实物 | M2 与 Legacy 源头 |
| `Knock` | 原敲门行为 | 仅 Legacy 门内应答 |
| `Rest` | 原休息行为 | 仅 Legacy 值班禁眠 |
| `TransportUnsealed` | 绑定线路的实际运输，检查未封装实物 | 仅 Legacy 南线 |
| `MoveInDark` | 移动前未照明；疾走逐格检查，同实例每行动最多一次 | 两个工厂均未投放。没有正式房间、名称、可靠线索或冻结伤害；点灯使此谓词不匹配，但不能因此虚构一条“黑暗通行规则” |
| `LightEnds` | 轮末有实际开启的灯光结束；没点灯不产生此事件 | 两个工厂均未投放。没有正式名称、可靠线索或冻结后果；模仿点灯也会留下真实灯光状态，不能视为灯灭事件豁免 |

[完整枚举](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/Models.cs#L12-L14)；[现场事件分支](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L528-L538)；[灯光结束事件](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L356-L360)。

`City / Line / Local` 三种范围都已定义，现场筛选与排序也预留全城规则，但当前工厂没有任何 `City` 实例。当前也只有前述普通敌影；“不可驱离的敌影”虽可由字段表达，却没有工厂/生成步骤投放的命名敌种。不要把数据默认伤害 2、允许驱离等默认值写成未设计异常的完整条目。[范围与默认字段](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/Models.cs#L152-L183)；[敌影字段](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/Models.cs#L272-L278)；[当前普通敌影生成](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L369-L374)。

## 规划、历史示例和缺口

完整版本计划从地点范围推进到线路、全城范围，主守则预算暂定同时最多 3 条；后期用线路/全城规则替换部分地方名额，不是无上限叠加。第一城测试传播频率为每两日最多一个目标，后两城每日最多两个；这些完整配置不等于当前固定 M2 已生成三城。[当前规划](https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/design/异常回收公司详细策划案_v0.2.md#L396-L423)。

| 尚未完成的条目 | 已知内容与边界 |
| --- | --- |
| 约 18 条规则池 | 只是完整版本预算；完整名称、触发、线索、兼容组和变体没有清单，不在本页补写剩余条目。[预算](https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/design/异常回收公司详细策划案_v0.2.md#L443-L445) |
| 稀有揭晓“专家批注” | 候选方案：消耗一次机会，揭晓当前地点一条允许且未确认规则，不发额外首次确认奖；无合法目标不消费。载体、获得频率尚未冻结，M2 不强制投放。Legacy 残页透镜是历史样例，不是 M2 掉落。[最新需求](https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/requirements/Joy_需求规格说明书_v0.3.md#L314)、[历史透镜](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/WorldFactory.cs#L134) |
| 历史“镜厅搜查损 2 心志”例子 | v0.1 用于说明模仿避行为惩罚，没有完整现行内容契约；不能在 M2 镜中住客之外另算一条镜厅诅咒。[历史语义示例](https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/design/异常回收公司详细策划案_v0.1.md#L277-L281) |
| 历史“未封装红色物品乘车传播”例子 | v0.1 用于说明模仿所得红箱仍受运输约束；颜色、目的节点、后果计数等完整契约未冻结。当前 Legacy 南线代码并不要求红色。[历史示例](https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/design/异常回收公司详细策划案_v0.1.md#L281) |
| 历史“未封装异常物跨区运输留下侵入”建议 | v0.1 第三城的可替换全城规则示例，要求高风险回收前提供证据与可行封装；未给出具体已投放实例，不是当前 M2 全城守则。[历史第三城示例及可替换声明](https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/design/异常回收公司详细策划案_v0.1.md#L310-L316) |

当前实施记录报告了 A/B 对照、次数和预览等自动检查，但首次真人推理、压力条件下的完整取舍、第二套成长组合、稀有揭晓完整内容以及三城连续可完成仍有未验收范围。无压力经营账表和多种子固定地图日程不能替代这些结论。[实现证据边界](https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/docs/implementation-m2.md#L79-L99)。

相关异常实物（座钟、回声样本、源头红箱、侧房灯，以及历史玻璃门牌、残页透镜）分别记在[实物与工具](items.md)；房间接入与路线见[地点](locations.md)。它们的存在不自动增加一条未定义的隐藏规则。
