# 合同、调查与工程服务

[返回 Wiki](README.md) · [经营与现场规则](mechanics.md) · [地点](locations.md) · [实物](items.md) · [异常守则](anomalies.md)

本文完整剧透。需求依据为 `693307638a7d9521b8ee185628d8d685b66e15b2` 的策划 v0.2 与需求 v0.3 对齐修订；“M2 可用”按 `9cbe156779aa38dbce0b798b5db0143df5282846` 只读核对。当前有 **3 个具名回收合同、2 个付费调查里程碑、1 个可选灯具收购、1 个公开合作点**；历史／对照调查和工程是执行方式或服务，不据此凑成“12 个委托”。完整产品的约 12 个模板仍是预算。[设计内容预算][D-budget] [M2 配置][W-map]

## 当前合同一览

所有出勤占 **2 人、1 事务**，固定账例预付 **1 资金**；默认无交通附加费。合同钱不是发现时到账：指定物须满足收容、成功撤出并真实交付。每个里程碑和实物交付只付一次，手操与委派共用进度。[需求业务][R-dispatch] [费用与人力][C-cost] [交付台账][C-deliver]

| 合同 | 地点与目标 | 调查里程碑 | 实物交付 | 收容与后续 |
| --- | --- | --- | --- | --- |
| **暮灯旅馆回收合同** | 暮灯旅馆·镜面房；[镜中住客的座钟](items.md#镜中住客的座钟)，2 格 | **0**；历史和对照调查均不付调查报酬 | **4** | 隔离工具 1 次；确认且归档镜中住客规程＋交付后可建设旅馆 |
| **白榆医院回收合同** | 白榆医院·回收室；[回声样本](items.md#回声样本)，1 格 | **2**，一份可靠记录足以支持该固定实例判断 | **5** | 本轮照明＋隔离工具 1 次；规程归档确认＋交付后可建设医院 |
| **红箱源头回收合同** | 红箱源头·回收室；[源头红箱](items.md#源头红箱)，3 格 | **2**，一份可靠记录足以支持该固定实例判断 | **8** | 源头专用箱 1 次，箱与红箱一起交付；不消耗便携隔离工具。源头不可建设，交付为交接必需 |

奖励、目标和收容配置见 [WorldFactory][W-targets]；最新需求明确医院／源头各一条规程、调查归档后次日使用。[最新合同表][R-m2]

### 暮灯旅馆回收合同

**M2 可遇到。** 首个档案柜是寻物记录，第二容器才有合同座钟。调查、观察并确认[镜中住客](anomalies.md)之后，仍要实际搜查、入包、封装、返回入口撤离；只取得记录或按中候选不交付实物。[两容器契约][R-mirror]

首次正确现场确认暂定 +1 心志、+1 零钱，零钱仅成功撤出才入公司；所以常见“旅馆总收入 5”是**合同 4＋现场确认零钱 1**，不是合同奖励 5。城市确认和全委派不发这份现场零钱。额外灯具的 6／2 报价是独立可选处置。[最新金额][R-m2] [城市确认][C-confirm]

手操可先取合同或只带证据撤离，未完成物保留回访；标准回收则须已归档确认规程。两种模式争用同一座钟和隔离工具，不能同日给两个队伍各回收一份；目标交付后合同不可重复。[回收受理][C-accept]

### 白榆医院回收合同

**M2 可遇到。** 保守调查返回病历室可靠记录，日终取得一次 **2 资金**里程碑并归档；下一准备阶段玩家确认“不要在黑暗中搜查”后可按规程委派。源头调查不需要等本合同完成，也不借用医院当日未归档的新情报。[医院规则][W-rules] [最新日程条件][R-m2]

样本必须照明封装，封装真实花隔离余量 1。标准委派当前检查队伍牌组含点灯能力；手操可以实际使用已安装灯具支援完成照明，但不能把委派的假想点灯当作灯具成长收益。交付 **5** 后仍需另付建设费，不会自动得到医院营业收入。[委派门槛][C-accept] [设计安装路线][R-routes]

### 红箱源头回收合同

**M2 可遇到，交接必需。** 调查归档、玩家确认源头回收规程后，才可从总部配发开局已公开预留的专用箱。标准回收可完成关键源头，不用隐藏成功率；必要规程和专用箱不齐则拒收。[箱配发][C-tools] [回收门槛][C-accept]

支付出勤费 1，收容并交付红箱和专用箱，获得 **8**；调查 **2** 是另一个一次性成果。专用箱随交付离开库存，不能改用作医院、旅馆或灯的容器。源头交付取消尚未落实的源头扩张，已经发生的运输入侵仍保留；源头和医院回收都不要求等期限最后一天。[交付实现][C-deliver] [最新需求][R-m2]

### 可选灯具收购

**M2 可遇到，可放弃。** 暮灯旅馆镜面侧房的[不灭床头灯／侧房灯](items.md#不灭床头灯侧房灯)只有一件，报价在出勤前公开固定为 **6 或 2**。搜索、放侧房、往返与封装费用照付；成功带回封装灯后可登记日终出售，处置前可取消，不占新增出勤／工程事务。[灯具定义][W-targets] [出售登记][C-lamp]

出售后唯一实物离开仓库，不可再安装或再出售。也可保留封装灯安装支援，或合法带回未封装灯暂存；未封装灯不能直接卖、安装或免费变成研究。这是同一资产的处置选择，不是三份可以同时领的奖励。[物品处置需求][R-lamp] [灯具全条目](items.md#不灭床头灯侧房灯)

### 公共协作点签约

**M2 可遇到，开局可建设。** [公共协作点](locations.md#雾港当前-m2-固定地图)无需异常实物和守则调查，支付 **4 资金、1 人、1 事务**建设。日终完成，次日起通过驻站、总部连通、维护与入侵检查后每日收入 1，并可参与封控与交接。[节点定义][W-map] [建设受理][C-accept]

它可作为所需两个有效营业点之一，提供全委派路线；不能代替源头交付。签约没有另发一次性现金合同，反复查看也不刷新建设资格。设施和已安装资产交接留给当地分公司；营业完整条件见[经营规则](mechanics.md)。

## 调查与回收方式

下面是业务方式，**不是额外具名合同池**。当日新调查结果统一日终归档，其他已锁定队伍不会提前拿到信息。[需求调查边界][R-dispatch] 每个节点同日只能安排一项出勤，手操、保守调查、对照调查和标准回收都占该目标；不能上午查完再假定下午新增一队。[目标与员工校验][C-plan]

| 方式 | 出现地点／条件 | 代价 | 成果、次数与限制 |
| --- | --- | --- | --- |
| **手操出勤** | 旅馆、医院、源头；可达、队伍基础配置合法，未知规则也可进场 | 2 人、1 事务、1 资金；每日最多一项手操 | 按实际证据、零钱、实物及损耗结算；可部分成果撤离、以后重返，回访不刷库存或奖励 |
| **保守调查：旅馆历史记录** | 暮灯旅馆；尚未取得旧出勤可靠录像 | 2 人、1 事务、1 资金；无调查收入 | 返回同实例历史录像，能排除“黑暗进入即受伤”，不能区分 A/B；不拿座钟、不耗隔离工具；记录已取得后不重复受理 |
| **保守调查：医院记录** | 白榆医院；病历室可靠记录尚未取得 | 同上 | 带回一份记录，日终首次调查 2；次日玩家判断。成功手操取得证据亦共享该一次性里程碑 |
| **保守调查：源头记录** | 红箱源头；收容可靠记录尚未取得 | 同上 | 带回一份记录，日终首次调查 2；次日可判断并配专用箱。独立于医院当日回收 |
| **对照调查：旅馆暗处首次搜查** | 历史记录已归档，尚无该实例可区分观察，原柜或登记备用样本尚存 | 2 人、1 事务、1 资金；不配发隔离工具，无调查收入 | 在新出勤暗处进行本房第 1 次正常搜查，只带观察／寻物记录，保留合同物；A 触发 2 反噬，被公开的 1 护符抵挡；B 不触发；模板足额防护，无额外恢复缺口 |
| **标准回收** | 旅馆、医院、源头；目标仍在现场、合同未付，所有地方／路线／全城必需规程已归档确认，真实工具与能力齐全 | 2 人、1 事务、1 资金＋实际封装次数 | 按合法模板回收指定唯一目标；医院需点灯能力，源头专用箱，其他目标隔离工具 1 次。不自动取灯、便条或稀有支路收益 |

手操和委派预览见 [业务说明][C-preview]；历史／对照受理见 [有限来源校验][C-contrast]；对照实际结果见 [执行代码][C-contrast-execute]。对照原柜用完只可消费预先登记的 **1 份备用样本**，不重刷原柜；已得到可区分观察后不再受理重复调查。候选由玩家自己确认。[样本定义][W-mirror]

**调查与确认不同。** 城市准备阶段每条规则每天最多提交一次；需已归档证据或观察，不发现场心志／零钱；错误保留且同一错误不可重复，城市当日已提交的该条不可再转现场提交。现场配额和全部答案见[异常守则](anomalies.md)。[城市确认实现][C-confirm] [最新要求][R-dispatch]

## 工程与经营服务

工程通常 **1 人、1 事务**，准备阶段安排、锁定预付、日终完成；驻站另占 1 人但不占事务。所有人数和费用都是当前测试参数。代码有 11 个事务枚举，其中扩编被 M2 禁用，研究存在接口和 UI 类型不一致，不能据枚举宣称全部对玩家开放。[事务枚举][M-jobs] [人力费用][C-cost]

### 接管建设

**M2 可用：旅馆、医院、公共协作点。** 费用 **4**。旅馆／医院先完成指定实物交付和归档确认规程；公共协作点按公开合作资格直接建设。总部、源头、已建点不可再建设；两枢纽没有初始合同或合作资格。[受理][C-accept]

当日日终建成，次日才可驻站营业、封控和计交接；不会补领当日营业钱。同一设施只建设一次，日后断联／入侵不是退回“未建成”。恢复靠维护或抢修，不重复付 4。[建设执行][C-build] [经营规则](mechanics.md)

### 常规维护

**M2 服务，出现待修复状态时可用；默认无压力账例通常不需要。** 在已建设、存在入侵或维护缺口、所需规程已归档确认的服务点受理；当前费用 **1**，占 1 人／1 事务，需已有合法通路。[维护门槛][C-accept] [费用][C-cost]

日终清除该点现有入侵及来源、恢复维护状态，设施保留；可在以后出现新缺口时再维护，不能对完好点刷工程。旧点若晨间留下驻站，日内修好、当晚仍有效，可参与当晚封控／交接但不补本日收入。不会重付旧合同或生成新实物。[维护执行][C-build] [经营资格详解](mechanics.md)

### 通路抢修／线路抢修

**条件性服务：存在相邻中断通路时才可受理。** 当前费用 **2**，1 人／1 事务；修复通行状态，保留线路守则，不清除服务点入侵，也不补营业收入。其他任务不能在同日锁定时借用尚未完成抢修的通路。[受理与同日通路][C-plan] [抢修门槛][C-accept]

**代码差异：** 设计的业务边界是修复指定通路；当前接口选择的是节点，一次完成会恢复该节点**所有**相邻中断边。默认 M2 没有强制断路；其压力预告也存在文字／执行差异，详见[压力配置](locations.md#压力配置相同地图另算经营后果)。只有真正出现中断时这项服务才有合法目标，不应把抢修费硬扣进无压力标准日程。[设计业务表][D-services] [实际抢修][C-build]

### 隔离工具补充

**M2 可用：公司总部唯一公开供应。** 指定[便携隔离工具](items.md#便携隔离工具)，费用 **3**、1 人／1 事务，消耗唯一[供应商替换耗材](items.md#供应商替换耗材)，工具 +1 次，最大 2。[最新工程规则][R-tools]

工具和耗材必须在可用仓库并一起预留，满额拒收；同一工具本日不可再分给出勤队或另一工程。日终补入、次日才可派用，不可预支到当日补封装。此供应整局仅一次，用后不刷新；不是反复花 3 就能无限补。[工程校验][C-engineering-check] [补充执行][C-engineering]

### 总部补封装

**M2 可用：合法归库的未封装侧房灯。** 1 人／1 事务、**无额外现金费**，预留并消耗已有隔离工具余量 1；日终完成，次日准备才可卖或安装。不接受必需合同物，不能在同日借刚补入的工具次数。[需求][R-lamp] [受理][C-engineering-check]

同一灯只能成功补封装一次；如果工程日终立即完成城市交接，不能追加旧城准备阶段的出售／安装。跨城仍携实物、保留状态是完整远征设计；M2 单城此时已结束。[执行][C-engineering] [单城结束][C-end]

### 灯具安装、卸下与支援

**M2 可用，属于准备配置，不另占工程事务。** 封装灯在有效驻站点安装，现金费 0；从当日锁定生效，并为一支手操队预留一份点灯支援。每天、该灯、该队出勤均只能使用一次；免费点灯不耗行动，照明与异动变化真实发生，保留模仿参考。[安装／预留][C-lamp] [点灯执行][F-light]

停业暂停未用支援，下一准备阶段可拆下归库；拆装不刷新额度。交接仍安装的灯留给当地分公司；不能同时售、研究、随队携带。每项条件及三种处置比较见[灯具条目](items.md#不灭床头灯侧房灯)。

### 研究：仅有引擎接口，当前正常 UI 未开放

**设计方向已明确，当前 M2 没有可玩研究成长样例。** 最新需求允许有处置权的物品用于研究，真实消耗并保留研究／解锁成果；具体奖励、能力和完整研究池未冻结。[研究需求][R-research]

只读实现核对：引擎支持总部研究事务，费用 0、1 人／1 事务，只接受仓库已封装的可选 `Lamp`／`Anomaly`，不接受合同物；完成后把物品标为已消耗，写研究日志，**没有新增能力或现金**。然而正常 UI 只给 `Rare` 类型显示研究按钮，M2 又没有投放 `Rare`，且引擎并不接受该类型。因此不能向玩家指引“拿灯点研究获能力”，也不能把 UI 确认文案中的“次日能力生效”当作实现收益。[引擎受理][C-engineering-check] [研究执行][C-engineering] [UI 类型条件][UI-research]

### 扩编：规划／历史支持，M2 禁用

完整方向暂定 **6 人增至 8 人**，付 **8**、1 人／1 工程事务，此后每日基本支出 +1，最多一次；它不自动增加事务位。当前 M2 校验明确禁止扩编，不能用它补救当日六人冲突。[设计人员][D-staff] [M2 拒绝扩编][C-accept]

历史执行分支添加周遥、方白两人并增加基本支出，不提供员工能力池或永久职业差异；不能据两个名字虚构新的员工职业内容。[历史扩编执行][C-recruit]

### 紧急支援

**M2 压力模式可用的测试缓冲，非客户合同。** 开启传播压力时，准备阶段每次验证远征最多领取一次 **+6 资金**，记录已使用，不占工程事务，也不提供收容次数。它不重复发放、不随次日刷新，评价权重尚未冻结。无压力默认局没有正常界面入口，不能把引擎通用方法视作可领取收入。[设计方向][D-support] [支援实现][C-emergency] [按钮显示条件][UI-emergency]

## 历史合同与仅定义内容

| 条目 | 历史内容 | M2 状态 |
| --- | --- | --- |
| 旅馆停摆座钟合同 | 调查默认 2、交付默认 5；暗处搜查与门内应答两条地方规则 | 由镜中住客／座钟合同取代，调查 0、交付 4；不能沿用旧 7 收入 |
| 医院回声样本合同 | 调查 2、交付 5；暗室病历与值班禁眠两条必需规则 | 仍为同名样本，现仅一条必需规程，新增照明封装硬条件 |
| 源头红箱合同 | 调查 2、交付 8，使用旧隔离工具；无专用箱 | M2 改用专用箱，别再扣隔离工具次数 |
| 旧城合作站 | 公开合作点，建设 4；连接旅馆与医院 | M2 为公共协作点，连接南街枢纽与医院 |
| 南线运输规程调查 | 历史南线中继有运输守则，可调查该线路记录；节点调查奖励为 0 | M2 无该规则／线路引用，压力开关不自动增加这份调查 |

历史配置见 [CreateLegacy][W-legacy]；默认调查金额见 [节点构造][W-default]，回收默认 5 见 [模型][M-node]。界面若给总部、枢纽展示通用“交付 +5”字段，也不代表存在可回收合同；必须有指定真实目标、资格和一次性台账才计收入。[目标受理][C-accept]

工具站替换／删牌／升级是**规划服务**，暂定每经营日一次、每次 2；当前准备界面的免费模板调整不是这项服务已完成。调查、工具维护、接应运输、回收仓储四种专业网点，额外运输服务、完整研究奖励和跨城协作仍无完整已投放逐项清单；不要凭“12 个委托模板”补出名字、收益或物品。[卡组服务设计][D-tool-service] [实现范围说明][I-services] [专业方向][R-special]

[W-map]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/WorldFactory.cs#L19-L35
[W-targets]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/WorldFactory.cs#L58-L82
[W-rules]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/WorldFactory.cs#L47-L52
[W-mirror]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/WorldFactory.cs#L36-L46
[W-legacy]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/WorldFactory.cs#L87-L138
[W-default]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/WorldFactory.cs#L141-L144
[M-jobs]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/Models.cs#L7-L14
[M-node]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/Models.cs#L79-L103
[C-cost]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L835-L838
[C-deliver]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L718-L740
[C-confirm]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L208-L229
[C-accept]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L568-L587
[C-tools]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L463-L548
[C-lamp]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L52-L128
[C-plan]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L494-L553
[C-preview]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L131-L155
[C-contrast]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L555-L567
[C-contrast-execute]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L618-L647
[C-build]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L662-L680
[C-engineering-check]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L588-L602
[C-engineering]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L682-L705
[C-recruit]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L707-L710
[C-end]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L341-L356
[C-emergency]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L377-L383
[UI-emergency]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Scripts/Main.cs#L222-L225
[F-light]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L221-L234
[UI-research]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Scripts/Main.cs#L674-L675
[R-dispatch]: https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/requirements/Joy_需求规格说明书_v0.3.md#L186-L200
[R-m2]: https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/requirements/Joy_需求规格说明书_v0.3.md#L790-L804
[R-mirror]: https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/requirements/Joy_需求规格说明书_v0.3.md#L773-L788
[R-routes]: https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/requirements/Joy_需求规格说明书_v0.3.md#L808-L847
[R-lamp]: https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/requirements/Joy_需求规格说明书_v0.3.md#L346-L348
[R-tools]: https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/requirements/Joy_需求规格说明书_v0.3.md#L342-L344
[R-research]: https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/requirements/Joy_需求规格说明书_v0.3.md#L340
[R-special]: https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/requirements/Joy_需求规格说明书_v0.3.md#L224
[D-services]: https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/design/异常回收公司详细策划案_v0.2.md#L139-L153
[D-staff]: https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/design/异常回收公司详细策划案_v0.2.md#L78-L90
[D-support]: https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/design/异常回收公司详细策划案_v0.2.md#L439
[D-tool-service]: https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/design/异常回收公司详细策划案_v0.2.md#L192-L194
[D-budget]: https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/design/异常回收公司详细策划案_v0.2.md#L441-L445
[I-services]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/docs/implementation-m2.md#L18-L31
