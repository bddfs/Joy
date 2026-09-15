# 实物、收容工具与灯具

[返回 Wiki](README.md) · [地点与房间](locations.md) · [合同与服务](contracts.md) · [卡牌](cards.md) · [异常守则](anomalies.md)

本文完整剧透。最新设计以 `693307638a7d9521b8ee185628d8d685b66e15b2` 的需求 v0.3 对齐修订为准；物品投放与实际处理只读核对实现提交 `9cbe156779aa38dbce0b798b5db0143df5282846`。M2 正常配置共有 **8 个唯一实物实例**：3 件必需合同物、1 盏可选灯、1 份便条、1 件隔离工具、1 个源头专用箱、1 份替换耗材。两种报价不是两盏灯；卡牌、护符、墨水、零钱与寻物记录不另计为仓库实物。[完整 M2 投放表][W-items] [状态模型][M-item]

## 先看实物归属

物品以唯一身份在房间、背包、仓库、安装点、交付、出售或消耗状态之间转移。发现不等于入包，入包不等于撤离，撤离不等于交付；拒绝入包或容量不足会把已发现物留在原房间，之后可花 1 行动拾取。放回当前房间不刷新库存或奖励。背包共 6 格，**随身工具同样占格**。[需求实物规则][R-items] [搜查与拾取][F-search] [容量与归属][F-item]

封装前先满足该物公开条件。基础封装 2 行动，封装牌按[卡面](cards.md)支付较少行动，但两者都消耗真实工具 1 次。封装关闭明确的外溢，保留物品身份与其他运输条件。失败时，全部携带物与带入装备遗落实际失败房间，可合法重返取回；已花次数不退，未撤出零钱丢失。[封装校验][F-seal] [现场失败][F-fail] [需求持久工具][R-tools]

跨出勤与同城回访保留唯一物品位置、封装状态和余量。**跨城是完整远征设计，M2 在单城交接结束**：仓库中有处置权的物品与工具按真实状态带走，已安装灯留给当地分公司；新一局重置实物库存，研究和解锁属于长期成果。当前不能从“交接时留在仓库”推断已经能进入第二城操作。[交接设计][R-meta] [当前单城结束][C-end]

## 当前 M2 实物总表

| 名称 | 初始位置 | 占格 | 主要用途 | 投放状态 |
| --- | --- | --- | --- | --- |
| [镜中住客的座钟](#镜中住客的座钟) | 暮灯旅馆·镜面房第二容器 | 2 | 旅馆合同交付 4 | M2 可遇到 |
| [回声样本](#回声样本) | 白榆医院·回收室 | 1 | 医院合同交付 5 | M2 可遇到 |
| [源头红箱](#源头红箱) | 红箱源头·回收室 | 3 | 源头合同交付 8，交接必需 | M2 可遇到 |
| [不灭床头灯／侧房灯](#不灭床头灯侧房灯) | 暮灯旅馆·镜面侧房 | 1 | 出售 6 或 2；安装点灯支援；合法暂存 | M2 可遇到，同一物品的设计名／客户端名 |
| [值班便条](#值班便条) | 暮灯旅馆·安全值班室 | 1 | 有限安全搜查库存，支持建立模仿参考 | M2 可遇到 |
| [便携隔离工具](#便携隔离工具) | 总部仓库 | 1 | 封装座钟、样本或灯；初始 2 次、上限 2 | M2 可遇到 |
| [源头专用箱](#源头专用箱) | 总部仓库，领取受规程门槛限制 | 1 | 只收容对应源头红箱；1 次 | M2 可遇到 |
| [供应商替换耗材](#供应商替换耗材) | 总部仓库 | 1 | 一次补充工程的有限供应 | M2 可遇到 |

前三件尺寸与唯一合同标记见 [目标设置][W-items]、[目标构造][W-target]；三件工具未另设尺寸，当前模型默认 1 格，不是没有体积。[物品默认值][M-item]

## 镜中住客的座钟

**M2 可遇到 · 必需合同实物 · 唯一一件 · 2 格。** 出现在[暮灯旅馆镜面房](locations.md#暮灯旅馆的四个房间)第二容器。首柜只定位它，不能第一次查完就直接拾取；后续搜查或模仿搜查才会发现并包含一次合法入包机会。拒绝入包后才可用基础拾取拿回。[投放][W-items] [两容器执行][F-search]

用便携隔离工具 1 次封装，成功撤离后日终交付 **4 资金**；调查不另给钱。若玩家尚未在城市或其他途径确认，首次现场正确确认镜中住客另得 1 零钱，必须成功撤出才能归入公司；这 1 不是座钟价值。交付与所需规程归档确认后可支付 4 建旅馆。[旅馆配置][W-hotel] [交付][C-deliver] [接管校验][C-accept]

收容本身无照明硬条件；搜查过程仍受[镜中住客 A/B](anomalies.md)约束。实物交付后离开库存，不可另售、研究或带至下一城；失败遗落、回访与切换手操／委派不复制座钟或合同。[需求唯一实物][R-items]

## 回声样本

**M2 可遇到 · 必需合同实物 · 唯一一件 · 1 格。** 位于[白榆医院回收室](locations.md#白榆医院的三个房间)。日终调查里程碑 **2** 与封装实物交付 **5** 是两个不同的一次性成果；医院仅一条必需规程。[医院定义][W-rules] [目标][W-items]

封装要求**本轮照明**，并消耗便携隔离工具 1 次。确认规程不会代替实际照明或收容；标准委派还检查队伍有点灯能力。已安装灯的免费支援可以在手操医院真实点亮本轮，支援不代付封装次数。[收容校验][F-seal] [标准委派门槛][C-accept] [灯支援](#不灭床头灯侧房灯)

样本交付后离开库存；合同和归档规程完成可解锁医院建设。它没有“重复取样”的刷新池，退包或失败只改变同一件样本的位置。[交付][C-deliver] [目标唯一标记][W-target]

## 源头红箱

**M2 可遇到 · 必需源头实物 · 唯一一件 · 3 格。** 位于[红箱源头回收室](locations.md#红箱源头的三个房间)。调查里程碑 **2**，封装交付 **8**；交付源头是单城交接的必要条件。[投放][W-items] [源头规程][W-rules]

只能使用对应[源头专用箱](#源头专用箱)收容，消耗专用箱 1 次，不额外消耗便携隔离工具。源头调查必须先归档并确认才可配发该箱；玩家不能在陌生手操现场凭“刚确认”立即从总部领取。红箱与消耗后的专用箱都必须真实带回，日终一起交付；只带回其中一个不会免费补发另一个。[专用箱配发][C-tools] [收容工具选择][F-item] [双实物交付][C-deliver]

未封装入包和继续携带移动会触发源头现场外溢，通常 2 心志，可由护符抵挡；封装后停止该物外溢。它不是“只要红色就丢失”规则，详见[异常百科](anomalies.md)。源头交付取消尚未执行的自身扩张，不能撤销已经发生的运输后果。红箱交付后不能再次出售、研究或作为跨城库存。[规则定义][W-rules] [携带处理][F-carry] [交付后果][C-deliver]

## 不灭床头灯／侧房灯

**M2 可遇到 · 可选异常装备 · 唯一一盏 · 1 格。** 策划称“不灭床头灯”，当前客户端与需求日程简称“侧房灯／可选灯”；属于同一安装样例。它位于[旅馆可选镜面侧房](locations.md#暮灯旅馆的四个房间)，沿用镜中住客，未另添第二条谜题。报价开局公开固定为 **6 或 2**，领取后不重抽。[设计名称][D-lamp] [实际投放][W-lamp]

| 处置 | 条件与代价 | 收益与生效时点 | 唯一性与跨场景 |
| --- | --- | --- | --- |
| **现场封装后出售** | 放侧房、往返、搜索照常花行动；封装消耗隔离工具 1 次；成功撤出归库 | 可登记日终出售，按固定报价到账；处置提交前可取消登记 | 日终出售后离开库存，不能安装、携带或再售。发现时不提前付款 |
| **封装后安装** | 归库后的下一准备阶段，在已生效、连通、维护正常且有驻站的服务点安装；无额外现金或工程事务 | 锁定前为手操队预留；每经营日最多给一队的一次出勤提供一次免费点灯：0 行动，本轮照明、异动 −1（最低 0），保留模仿参考 | 安装后不占出勤背包，也不能同时出售或研究；停业暂停未用支援；拆装、换队、读档不刷新已用日额度 |
| **未封装暂存** | 满足现有运输条件，合法带回仓库 | 当日不卖、不安装、不获得研究或支援；保留后续选择 | 灯仍未封装。总部补封装另占 1 人／1 事务、已有隔离次数 1，无现金费，日终完成、次日才可出售或安装 |
| **拆下归库** | 后续准备阶段取消支援预留后拆卸 | 重新成为仓库实物，可按合法条件再配置 | 当日已用支援额度保留；交接时仍安装则留当地分公司，仓库实物跨城仅为完整远征设计 |

处置的设计约束见 [FR-ITEM-08／FR-NODE-07][R-lamp]；实现见 [出售与安装][C-lamp]、[实际点灯][F-light]、[补封装][C-engineering]。**当前正常 UI 没有把这盏灯开放为研究物**，不能把研究接口存在写成可用的第四种收益；详见[研究服务](contracts.md#研究仅有引擎接口当前正常-ui-未开放)。

额外封装灯会占去医院原本需要的工具次数，可能迫使次日安排补充工程。固定日程中，高报价出售路线第 5 日余 22，低报价余 18；保留工具路线第 4 日余 20；安装路线第 5 日余 16 并实际使用支援。最新需求纠正了策划原表的算术，不能继续引用 24／20／18 的旧终值。[校正日程][R-routes] 详细日程见[经营规则](mechanics.md)。

## 值班便条

**M2 可遇到 · 普通记录实物 · 唯一一份 · 1 格。** 在暮灯旅馆安全值班室，搜查发现后可入包；无需封装，无现金或合同奖励。它的作用是提供一份真实、安全的搜查库存，让玩家打出搜查牌后保留参考，前往镜面房模仿搜查。[投放与说明][W-note] [安全房说明][W-hotel]

取走后不会因换房、换队或重返再长出一份；无须为了合同带走它，拒绝入包仍保留已发现物。作为普通记录，它没有被定义为可售灯具、可研究异常物或稀有揭晓工具，不自动确认规则。跨城没有独立奖励约定，适用仓库实物的一般设计边界。[搜查与拒绝][F-search] [处置类型校验][C-engineering-check]

## 便携隔离工具

**M2 可遇到 · 唯一实例 `seal-a` · 1 格 · 初始 2／最大 2 次。** 开局在总部仓库；用于旅馆座钟、医院样本和可选灯各 1 次。牌只改变行动费，不能创造收容工具。即使余 2 次，同一工具也不能同日交两队；同一队可以按真实余量先后封装两物。[定义][W-tools] [需求][R-tools]

准备时预留真实实例，手操开始进入背包；成功封装即时扣次数，正常撤出回仓。用尽后工具仍然存在，可通过[有限补充](contracts.md#隔离工具补充)增加一次；满额不受理。失败、换人、回访、换城与已提交检查点读档都不补回次数。失败会遗落工具，允许空配装重返拾取，不能以工具不在仓库为由生成新副本。[真实消耗][F-search] [空配装重返][C-empty] [失败][F-fail]

**代码边界：** 主动随行工具的计划校验要求有正余量（补充工程目标例外），所以已耗尽工具不能当普通随行装备重新配出；遗落的已发现实例仍可现场拾取。工具的跨城携带只有设计要求，M2 尚无第二城。[配装校验][C-tools]

## 源头专用箱

**M2 可遇到 · 唯一实例 `source-case` · 1 格 · 1 次。** 开局就在总部仓库，是公开合同预配资源；只有对应源头规程已归档并确认后才能领取预留，不是资源耗尽时临时赠送。[定义][W-tools] [配发条件][C-tools]

只能收容源头红箱，不能替代隔离工具封装旅馆、医院或灯。使用后保留空余量为 0 的实物，随源头一起交付；若专用箱遗落，须真实取回才能完成源头合同。未交付前属于公司或队伍的唯一资产，交付后不留作跨城工具，不可重复领取。[工具选择][F-item] [共同交付][C-deliver]

## 供应商替换耗材

**M2 可遇到 · 唯一实例 `supply-charge` · 1 格 · 1 次。** 开局总部库存，用于唯一一次公开补充供应。工程付 **3 资金、1 人、1 事务**，预留便携隔离工具和耗材，日终消耗耗材、工具 +1 次，次日才可分配；工具上限 2。[定义][W-tools] [需求工程][R-tools]

这不是现场可立即补充的卡。满额、工具不在可用仓库或唯一供应已耗尽时不受理；不能同时把该工具派给另一队，也不能用同日日终预期补入的次数做另一项补封装。耗材用后进入“已消耗”，不刷新；未用耗材的后续城供应安排尚未形成完整内容池。[受理与占用][C-tools] [执行][C-engineering]

## 档案柜寻物记录与备用对照样本

**M2 可遇到，但不是两件额外背包实物。** 镜面房原档案柜只有 1 次库存，搜索后给寻物记录并定位座钟的第二容器，不给现金、不直接发现座钟。普通和模仿搜查共用这份真实库存；重返不刷新。[规则契约][R-mirror] [执行][F-search]

原柜已用时，旅馆对照调查可使用**事先登记的另一份备用对照柜样本，初始 1 份**。它只由调查模板消费，未定义为可进入房间或可携带物品；若已经有可区分观察，也不能继续受理同实例对照调查。[样本定义][W-mirror] [模板受理][C-contrast] [对照执行][C-contrast-execute]

## 历史实物与 M2 未投放项

以下均来自 `CreateLegacy`，**已定义但未投放于当前 M2**。旧七件实物为三件合同物、玻璃门牌、残页透镜、封装工具 A、封装工具 B；不是 M2 八件之外还能获得的隐藏池。[完整历史投放][W-legacy]

| 历史条目 | 出现地点、尺寸与旧用途 | 次数、归属与当前边界 |
| --- | --- | --- |
| **停摆座钟** | 暮灯旅馆·旧客房，2 格；旧旅馆合同实物，默认交付 5、调查 2 | M2 同内部目标替换为镜中住客的座钟，合同改 4、调查 0；不可将两名称计为两件同时出现的物 |
| **回声样本（旧配置）** | 白榆医院·回收室，1 格；调查 2、交付 5 | 旧配置未设置“封装必须照明”字段；M2 新增该硬条件 |
| **源头红箱（旧配置）** | 红箱源头·回收室，3 格；调查 2、交付 8 | 旧配置使用隔离工具；没有独立源头专用箱 |
| **玻璃门牌** | 暮灯旅馆·镜厅，1 格，标示价值 2，可选异常物 | 一件真实库存，撤离进入仓库；当前出售接口只接受灯，不能仅凭 `Value=2` 宣称玩家能出售它换 2。非合同物，可封装，但未投放于 M2 |
| **残页透镜** | 暮灯旅馆·未拼接侧房，1 格，1 次揭晓机会 | 取得后消耗 1 次与 1 行动，揭晓当前现场一条尚未确认规则；不发普通确认奖励，无目标不扣机会；消耗不随回访刷新。当前 M2 无该掉落 |
| **封装工具 A** | 旧总部仓库，1 格，2 次 | 旧唯一实例 `seal-a`，M2 变为便携隔离工具；不是额外赠送的第三、四次余量 |
| **封装工具 B** | 旧总部仓库，1 格，2 次 | 独立 `seal-b` 可供另一队，但 M2 完全未投放，不能用旧双工具攻略排同日旅馆＋医院回收 |

旧奖励默认值见 [节点构造][W-default]、[模型默认奖励][M-node]。透镜处理见 [揭晓执行][F-reveal]、[无目标校验与行动费][F-reveal-check]。玻璃门牌的价值字段和其现实可处置能力分开记录，不能据字段编造收益。

## 规划与缺口

“专家批注”是稀有揭晓的**候选设计**：消耗一次机会，揭晓当前地点允许的一条未确认规则，不另发普通确认奖，无目标不耗；载体、来源与频率未冻结，M2 未投放。“约 12 件异常物”是完整版本预算，当前没有可完整枚举的 12 件名称／效果表；异常装备、工具升级、借用牌与第二套组合也不能凭预算补造。[稀有揭晓需求][R-reveal] [内容预算][D-budget] [实现缺口记录][I-gap]

[W-items]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/WorldFactory.cs#L58-L82
[W-target]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/WorldFactory.cs#L179-L185
[W-hotel]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/WorldFactory.cs#L58-L67
[W-rules]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/WorldFactory.cs#L47-L52
[W-lamp]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/WorldFactory.cs#L75-L76
[W-note]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/WorldFactory.cs#L73-L74
[W-tools]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/WorldFactory.cs#L77-L82
[W-mirror]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/WorldFactory.cs#L36-L46
[W-legacy]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/WorldFactory.cs#L129-L136
[W-default]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/WorldFactory.cs#L141-L144
[M-node]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/Models.cs#L79-L103
[M-item]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/Models.cs#L186-L211
[F-search]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L154-L194
[F-seal]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L414-L435
[F-item]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L486-L520
[F-fail]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L577-L585
[F-carry]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L522-L554
[F-light]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L221-L234
[F-reveal]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L202-L214
[F-reveal-check]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/FieldEngine.cs#L444-L469
[C-deliver]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L718-L740
[C-accept]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L568-L587
[C-tools]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L463-L548
[C-empty]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L110-L120
[C-lamp]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L52-L128
[C-engineering]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L682-L705
[C-engineering-check]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L588-L602
[C-contrast]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L555-L567
[C-contrast-execute]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L628-L647
[C-end]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/Core/CityEngine.cs#L341-L356
[R-items]: https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/requirements/Joy_需求规格说明书_v0.3.md#L330-L340
[R-tools]: https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/requirements/Joy_需求规格说明书_v0.3.md#L342-L344
[R-lamp]: https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/requirements/Joy_需求规格说明书_v0.3.md#L346-L348
[R-meta]: https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/requirements/Joy_需求规格说明书_v0.3.md#L350-L360
[R-routes]: https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/requirements/Joy_需求规格说明书_v0.3.md#L808-L847
[R-mirror]: https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/requirements/Joy_需求规格说明书_v0.3.md#L773-L788
[R-reveal]: https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/requirements/Joy_需求规格说明书_v0.3.md#L314
[D-lamp]: https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/design/异常回收公司详细策划案_v0.2.md#L283-L289
[D-budget]: https://github.com/bddfs/Joy/blob/693307638a7d9521b8ee185628d8d685b66e15b2/docs/design/异常回收公司详细策划案_v0.2.md#L441-L445
[I-gap]: https://github.com/bddfs/Joy/blob/9cbe156779aa38dbce0b798b5db0143df5282846/docs/implementation-m2.md#L94-L99
