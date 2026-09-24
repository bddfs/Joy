# 卡牌与基础行动

[Wiki 首页](README.md) · [异常与守则](anomalies.md) · [物品](items.md) · [机制](mechanics.md) · [地点](locations.md)

本页包含完整玩法剧透。收录 **10 种基础卡、7 种当前基础／支援行动、4 种免费操作**，并单列历史稀有揭晓接口和未完成的成长内容。规则以 2026-09-15 修订的[需求 v0.3][req-cards]为准；“M2”专指开发提交 [`9cbe156`][m2-record] 的固定单城实现，不能理解为主分支已合并或完整游戏已发行。以下行动费与构筑数值仍是验证参数。

状态用语：**M2 可用**表示当前场景有对应操作；**M2 可配、无目标**表示卡牌存在但当前内容没有合法对象；**规划未实现**表示需求保留、当前没有完整玩法；**历史未投放**表示旧场景或接口仍可在源码中看到，当前 M2 不提供其获得途径。

## 开局、抽牌与构筑

起始 **12 张、10 种**：调查和搜查各 2 张，其余各 1 张。M2 准备阶段可在“队伍基础牌组”免费增减，范围 **10–16 张**，**模仿最多 1 张**；锁定后不能调整。其他基础牌当前没有逐种副本上限，只受总张数限制。这个免费模板编辑是 M2 的配置实验；付费工具站服务另列在本页末尾。[起始牌组][deck]、[准备界面][deck-ui]、[实现边界][m2-deck]

每轮重置为 **3 行动**，抽取 **5 张**。普通已用牌进入弃牌堆，原手牌槽留空，其余牌不挪位；轮末未用手牌也弃置，下一轮重新填五槽。牌库不够时只重洗弃牌堆补足，当前手牌与耗尽牌不会被洗入；牌库、弃牌都空就停止抽牌。**模仿成功打出后本次出勤耗尽，未打出的模仿仍正常弃置。**[需求][req-round]、[抽牌与固定槽][draw]、[用牌去向][play]、[轮末][end-round]

卡牌需在手中、行动足够且有合法目标才能使用。无效目标先说明原因，不扣行动、不弃牌、不耗尽。查看地图上的另一房间只是查看；除移动牌外，卡牌作用于队伍当前房间或当前现场允许调查的内容。[目标校验][validate]、[界面位置说明][field-ui]

## 基础卡全表

表中“可模仿”指该牌能提供模仿指令；仍须在模仿时重新满足当前目标的条件。所有卡均可通过 M2 准备阶段模板配置，起始张数见下表。[基础效果需求][req-base]、[白名单需求][req-mimic]

| 卡牌／状态 | 行动费／起始 | 基础效果与适用范围 | 模仿边界 | 来源 |
| --- | --- | --- | --- | --- |
| **调查** · M2 可用 | 1／2 张 | 取得当前允许调查的一条尚未取得的可靠线索，供玩家提出假说；不会自动确认守则。M2 按当前现场适用规则取首条未获线索；没有剩余线索不可用。旅馆入场已自动提供镜中住客的旧录像，不能再用调查重复领取这份记录。 | 可模仿；重新查当前可取得的线索，不复制旧记录、不额外生成线索。 | [卡面][card-investigate]、[实际调查][investigate]、[入场记录][start]、[无目标判定][limits] |
| **搜查** · M2 可用 | 1／2 张 | 搜索当前房间真实容器，包含当次合法领取。普通搜查使“本房本次出勤正常搜查”加 1；拒绝入包或容量不足时，已经发现的实物留在原房，搜查费用照付。镜面房第一柜只给寻物记录并定位第二容器，不能直接拿到座钟。 | 可模仿；读取当前位置库存，不增加正常搜查次数，但依然消耗柜子／物品的真实库存，且取得、携带后果照常发生。 | [卡面][card-search]、[搜查结算][search]、[容器目标][targets] |
| **敲门** · **M2 可配、无目标** | 1／1 张 | 从允许应答的房门取得一份真实库存。必须同时有允许应答的门和可取库存；当前 M2 没有允许敲门的房间，所以无法合法打出。历史旅馆镜厅曾允许敲门。 | 白名单内，但模仿仍要求当前门允许应答；不能用模仿绕过门的物理资格。成功模仿不产生原敲门行为，仍处理实际取得与携带。 | [卡面][card-knock]、[目标校验][search-validation]、[M2 禁止应答][safe-room]、[历史房门][room-factory] |
| **点灯** · M2 可用 | 1／1 张 | 开启本轮照明，异动 −1，最低 0；照明在轮末结束。适合已确认需要照明的搜查、移动或封装条件，但不会把与灯光无关的规则变安全。 | 可模仿；保留实际灯光变化和异动变化，轮末照样熄灯。模仿并不复制上一次点灯的最终结算数字。 | [卡面][card-light]、[点灯结算][light]、[轮末][end-round] |
| **休息** · M2 可用 | 1／1 张 | 心志 +2，上限 12；每个房间实例最多 2 次。上限以上的恢复截断，离房重进不能恢复额度；M2 在地点房间实例上保存休息次数，重返出勤也不重置。 | 可模仿；普通与模仿共用同一房间的 2 次额度；模仿不产生原休息行为，实际恢复仍发生。 | [卡面][card-rest]、[恢复结算][rest-ward]、[额度判定][limits]、[实例状态][room-state] |
| **护身** · M2 可用 | 1／1 张 | 护符 +1，上限 3，满额不能使用。每枚只能抵挡一项标为可抵挡的负面规则或普通敌影攻击；行动成本、异动阈值袭击和封门不能抵挡。 | 可模仿；共用护符上限，不创建第二条护符槽。 | [卡面][card-ward]、[恢复结算][rest-ward]、[护符范围][req-ward] |
| **疾走** · M2 可用 | 1／1 张 | 沿已放置且相邻连通的路径移动 **1–2 格**，逐格结算。每格仍检查移动、携带等后果；同一规则实例在这一次疾走中最多触发一次，不同实例分别检查。途中归零立即结束。 | **不可模仿，也不覆盖既有参考。** 没有参考时使用疾走不会建立参考。 | [卡面][card-sprint]、[合法路径][move-validation]、[逐格执行][movement]、[规则上限][req-sequence] |
| **模仿** · M2 可用 | 1／1 张 | 在当前位置执行最近合资格参考的基础指令；成功后进入本出勤耗尽区。每队最多配 1 张，不额外损失心志。 | 不可模仿自身；不覆盖参考。无参考、参考不在白名单或当前目标不合法时不可用，不消耗牌。完整边界见下节。 | [卡面][card-mimic]、[可用性][validate]、[需求][req-mimic] |
| **封装** · M2 可用 | 1／1 张 | 封装背包中一个未封装且满足收容条件的物品，消耗真实工具 1 次。医院回声样本要求本轮照明；源头红箱必须用已配发的专用箱，不能换用便携隔离工具。封装不会直接交付合同。 | **不可模仿，但会覆盖参考。** 打出后不能回找此前的搜查；需要保留参考时可考虑 2 行动的基础封装。 | [卡面][card-seal]、[收容校验][seal-validation]、[工具与目标][seal-target]、[封装执行][seal-execute] |
| **驱离** · M2 可用 | 1／1 张 | 清除当前房间一个明确可驱离的普通敌影。敌影会先预告，最早出现后的下一轮末攻击；也可离开其作用房间或用护符抵挡。没有合法敌影时不可用，不能解除守则诅咒。 | **不可模仿，但会覆盖参考。** 后续模仿不可用，直到再打出白名单牌。 | [卡面][card-banish]、[清除与校验][banish]、[敌影出现][shadow]、[敌影需求][req-ward] |

## 模仿怎么用

白名单只有 **调查、搜查、敲门、点灯、休息、护身**。参考是最近一张会更新参考的行动牌，不是“最近一张能模仿的牌”。因此封装牌或驱离牌会留下不可模仿的参考，不能自动向前寻找搜查。[需求][req-mimic]、[卡牌属性][catalog]

| 中间发生的操作 | 对已有参考的影响 | 能否作为新参考 |
| --- | --- | --- |
| 打出调查／搜查／敲门／点灯／休息／护身 | 替换为本次指令 | 能，进入白名单 |
| 打出封装牌／驱离牌 | 覆盖旧参考，模仿变为不可用 | 会记录，但不能模仿 |
| 疾走、模仿自身 | 保持 | 不能 |
| 基础移动、徒手搜查、拾取、放回、基础封装、放房、网点点灯支援 | 保持 | 不能；它们不是行动牌 |
| 查看、提交候选、换轮 | 保持 | 不能 |
| 新出勤 | 清空 | 重新打牌建立 |
| 换层 | **需求要求清空；M2 没有换层玩法** | 后续楼层重新建立 |

来源：[参考规则][req-mimic]、[唯一更新入口][play]、[新出勤状态][start]、[换轮保留][end-round]、[单城实现范围][m2-record]。

模仿复用的是**基础指令**，不是上一张牌整套结算。它不复制旧房间的物品、最终数字、规则追加奖励、抽牌、行动点、移动或再次模仿；不产生原牌的搜查、敲门、休息等行为，但真实获得、携带、灯光变化、恢复和消耗仍然存在。背包容量、剩余库存、物理资格、收容及运输限制都不豁免。[需求][req-mimic]、[实际搜查事件][search]

例如在安全值班室打出搜查，取得唯一值班便条，再沿合法通路疾走到有库存的镜面房，仍可花 1 行动模仿搜查。前面的搜查、疾走、模仿合计 **3 行动**，还须实际抽到这三张牌；如果先打封装牌，参考会被覆盖。如果只做徒手搜查，则从一开始就没有建立搜查参考。[安全房库存][safe-room]、[牌费][catalog]、[参考需求][req-mimic]

对镜中住客而言，普通／徒手搜查会计正常次数，模仿搜查不计。因此模仿能避开 A、B 两种真值的正常搜查触发，但“模仿没受伤”本身不能区分 A 与 B。调查和试探方法见[异常百科](anomalies.md)。[对照规则需求][req-contrast]

## 不依赖抽牌的行动

下表 7 项均为 **M2 可用**，无需先获得卡牌；依然需要满足地点、实物或已分配支援的资格。它们都不创建、不覆盖模仿参考。免费查看不会使队伍移动。[基础操作需求][req-basic]、[M2 费用表][cost]

| 行动 | 费用 | 效果、适用／可用范围与边界 | 来源 |
| --- | --- | --- | --- |
| **基础移动** | 1 行动 | 移动到一格相邻、已放置的连通房间；第一次进入该房间获得 1 墨水。照常结算经过位置的移动与携带后果；不是模仿指令。 | [校验][move-validation]、[执行][movement] |
| **徒手搜查** | 2 行动 | 无搜查牌也可搜索当前合法容器，计一次本房本出勤正常搜查；库存、领取／拒绝、容量和规则与普通搜查一致。可用于试探，不能建立搜查参考。 | [搜查结算][search]、[目标][search-validation]、[需求][req-contrast] |
| **拾取** | 1 行动 | 仅拾取队伍当前房间已经发现的遗落物，需有容量。不会重新搜查、刷新库存或重领发现奖励；重新入包仍有实际获得／携带结果。 | [执行][pickup-drop]、[目标与容量][pickup-validation] |
| **放回当前房间** | 0 行动 | 将背包中一件实物放在当前位置，保留为已发现实物；不恢复工具次数、原始库存或奖励。之后拾回仍需支付拾取费。 | [费用][cost]、[执行][pickup-drop]、[实物状态][put-down] |
| **基础封装** | 2 行动＋工具 1 次 | 不抽到封装牌也能封装合法背包物；与封装牌共用照明／专用箱等收容要求和真实工具余量。多付 1 行动但保留模仿参考。 | [需求][req-basic]、[校验][seal-validation]、[执行][seal-execute] |
| **放置房间／放置侧房** | 1 行动＋1 墨水 | 仅旅馆：放置当前位置相邻的未放置房间。M2 对应镜面侧房；放房不移动队伍，仍要另付移动费进入。初始 2 墨水，首次进入新房间 +1。 | [参数][req-params]、[地点判定][place-validation]、[执行][place-execute] |
| **网点免费点灯** | 0 行动＋本次支援额度 | 需先取得、封装并撤出侧房灯，准备阶段安装在有效服务点，再给手操队预留支援。本轮照明、异动 −1；照常产生点灯与灯光事件。每灯具每经营日最多支援一队的一次出勤、使用一次，换队／拆装／读档不刷新。失效网点暂停未用支援。 | [实现与获得][lamp-support]、[使用校验][support-validation]、[执行][light] |

下列 4 种操作也不消耗卡牌，行动费均为 **0**；其中换轮和提交候选有各自后果，“免费”不代表没有风险。

| 免费操作／状态 | 可用范围与结果 | 模仿边界／来源 |
| --- | --- | --- |
| **查看地图、房间、证据、已知规则、物品、日志及行动预览** · M2 可用 | 活动现场可查看；点地图只改变查看对象，选中动作后仍须执行才付费。取消或关闭预览不结算。 | 保持参考。[需求][req-basic]、[界面][field-ui] |
| **提交守则候选** · M2 可用 | 需相关可靠线索或实际观察，全队每轮合计最多 1 次。首次正确 +1 心志（上限 12）、+1 零钱；错误 +2 异动并立刻检查阈值。已确认、已试错的同一候选不能重交；城市当日已提交的该条现场也不能重交。不是“调查牌的免费附加确认”。 | 保持参考，不能模仿提交或复制奖励。[需求][req-confirm]、[执行][confirm] |
| **结束本轮** · M2 可用 | 依次处理当前房间敌影攻击、灯光结束、异动 +1 与阈值袭击、封门；仍存活且未封门才进入下一轮、重置行动并抽牌。当前层限 12 轮，最后一轮在入口也应先撤离。 | 保持参考；换轮不能让耗尽模仿回来。[轮末][end-round]、[预览][end-preview] |
| **从入口撤离** · M2 可用 | 队伍存活并回到入口即可，不要求收齐合同物或深层封印。实物和零钱需要归队结算，合同按条件交付；运输规则另行检查，详见[机制](mechanics.md)。 | 不能模仿撤离；下次新出勤参考清空。[需求][req-basic]、[撤离判定][withdraw] |

## 规划内容与历史边界

| 内容／状态 | 费用、获得方式与适用边界 | 当前 M2 情况／来源 |
| --- | --- | --- |
| **新工具候选、永久获得与借用** · 规划未实现 | 暂定 3 选 1 或拒绝；现场找到的牌可免费进手牌，打出仍支付牌费，奖励点只领一次。借用仅本次出勤有效，其余先进入公司工具库再选择装入。 | 当前没有完整候选／领取／拒绝／借用流程。源码有额外手牌区的显示分支，不代表这套成长玩法已投放或已验收。[需求][req-cards]、[额外区代码][extra-hand]、[未完成记录][m2-growth] |
| **工具站替换、删牌、升级** · 规划未实现 | 暂定每次 2 资金，每经营日最多 1 次；删牌不得低于 10 张。升级须明确改变哪项用途、条件、事件及共享次数。 | 当前免费增减模板不是上述付费服务；也没有命名的升级牌清单可列为已获得内容。[需求与参数][req-cards]、[参数][req-params]、[实现边界][m2-deck] |
| **两套构筑成长组合** · 规划未实现完整范围 | 由工具升级、异常装备承载；每项需明确来源、触发、消耗、周期及普通／模仿共享规则，不增加独立遗物系统。 | M2 只完成侧房灯安装支援一套样例，不能据此声称两套组合或完整成长池已完成。[需求][req-cards]、[未完成记录][m2-growth] |
| **专家批注** · 规划候选，载体未定 | 候选效果：消耗 1 次机会，揭晓当前地点一条允许且未确认的规则，不额外发首次确认奖励；无合法目标不消耗。行动费、载体和获取频率未冻结，不能预设它是一张牌。 | M2 主对照样例不强制投放；未列入十种基础卡。[稀有揭晓需求][req-reveal] |
| **稀有揭晓／残页透镜** · 历史未投放 | 保留接口费用为 **1 行动＋1 次揭晓机会**，选择当前现场一个未确认规则实例，直接确认、不发普通确认奖励；无合法目标先拒绝，不扣费。历史透镜在旅馆侧房，1 格、1 次；不是卡牌，也不创建／覆盖模仿参考。 | 当前 M2 没有透镜库存或正常获得途径。不得把历史接口费用当作未来“专家批注”的冻结费用，也不得假定能揭晓全地图、复制物品或自动完成合同。[历史透镜][legacy-lens]、[M2 物品表][m2-items]、[费用][cost]、[校验][reveal-validation]、[执行][reveal-execute] |

需要留意的版本差异：敲门保留在起始牌组却没有 M2 合法房门；换层清空参考是需求规定，但固定单层尚无换层操作；付费调整、借用与稀有揭晓完整投放尚未完成。上述差异描述当前可玩边界，不修改最新需求，也不把历史 Demo 的“模仿可重洗”或“每房一次并扣心志”带入 M2。[模仿需求][req-mimic]、[当前实现限制][m2-growth]

[req-cards]: https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/requirements/Joy_需求规格说明书_v0.3.md#L280-L294
[req-round]: https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/requirements/Joy_需求规格说明书_v0.3.md#L264
[req-basic]: https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/requirements/Joy_需求规格说明书_v0.3.md#L266-L272
[req-ward]: https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/requirements/Joy_需求规格说明书_v0.3.md#L276-L278
[req-confirm]: https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/requirements/Joy_需求规格说明书_v0.3.md#L304
[req-sequence]: https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/requirements/Joy_需求规格说明书_v0.3.md#L306-L310
[req-reveal]: https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/requirements/Joy_需求规格说明书_v0.3.md#L314
[req-contrast]: https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/requirements/Joy_需求规格说明书_v0.3.md#L316
[req-mimic]: https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/requirements/Joy_需求规格说明书_v0.3.md#L320-L326
[req-params]: https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/requirements/Joy_需求规格说明书_v0.3.md#L465-L482
[req-base]: https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/requirements/Joy_需求规格说明书_v0.3.md#L486-L501
[catalog]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CardCatalog.cs#L8-L25
[card-investigate]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CardCatalog.cs#L16
[card-search]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CardCatalog.cs#L17
[card-knock]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CardCatalog.cs#L18
[card-light]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CardCatalog.cs#L19
[card-rest]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CardCatalog.cs#L20
[card-ward]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CardCatalog.cs#L21
[card-sprint]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CardCatalog.cs#L22
[card-mimic]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CardCatalog.cs#L23
[card-seal]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CardCatalog.cs#L24
[card-banish]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CardCatalog.cs#L25
[deck]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CardCatalog.cs#L28-L32
[start]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L19-L60
[play]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L122-L139
[movement]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L140-L149
[search]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L153-L175
[pickup-drop]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L177-L186
[seal-execute]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L188-L195
[place-execute]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L196-L201
[reveal-execute]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L202-L214
[investigate]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L215-L219
[light]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L221-L233
[rest-ward]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L235-L245
[banish]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L247-L250
[confirm]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L259-L287
[withdraw]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L290-L300
[end-preview]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L302-L343
[end-round]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L346-L376
[shadow]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L369-L373
[validate]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L379-L400
[move-validation]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L401-L412
[search-validation]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L414-L418
[pickup-validation]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L419-L428
[seal-validation]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L429-L436
[place-validation]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L437-L443
[reveal-validation]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L444-L448
[support-validation]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L449-L455
[limits]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L457-L460
[cost]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L464-L470
[targets]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L486-L491
[seal-target]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L493-L498
[put-down]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L511-L520
[draw]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L596-L627
[room-state]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/Models.cs#L213-L231
[field-ui]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Scripts/Main.cs#L334-L376
[extra-hand]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Scripts/Main.cs#L381-L387
[deck-ui]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Scripts/Main.cs#L615-L626
[safe-room]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/WorldFactory.cs#L58-L74
[m2-items]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/WorldFactory.cs#L69-L82
[legacy-lens]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/WorldFactory.cs#L134
[room-factory]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/WorldFactory.cs#L159-L175
[m2-record]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/docs/implementation-m2.md#L3-L16
[lamp-support]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/docs/implementation-m2.md#L26-L28
[m2-deck]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/docs/implementation-m2.md#L31
[m2-growth]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/docs/implementation-m2.md#L94-L101
