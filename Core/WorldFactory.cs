using System;
using System.Collections.Generic;
using System.Linq;

namespace Joy.Core;

/// <summary>A declared, fixed single-city validation scenario, not a random three-city generator.</summary>
public static class WorldFactory
{
    public static GameState Create(int seed = 413, bool? mirrorVariantB = null, int lampPrice = 6, bool pressure = false)
    {
        if (lampPrice is not (2 or 6)) throw new ArgumentOutOfRangeException(nameof(lampPrice), "M2 公开报价只使用 2 或 6。");
        bool variantB = mirrorVariantB ?? (seed % 2 == 0);
        var state = new GameState { Scenario = "M2", Seed = seed, RandomState = unchecked((ulong)(uint)seed) + 1, PressureEnabled = pressure };
        state.Employees = Enumerable.Range(1, 6).Select(i => new Employee
        { Id = $"e{i}", Name = new[] { "林舟", "陈墨", "许灯", "陆宁", "沈青", "顾川" }[i - 1] }).ToList();
        state.TeamDeck = new() { CardKind.Investigate, CardKind.Investigate, CardKind.Search, CardKind.Search,
            CardKind.Knock, CardKind.Light, CardKind.Rest, CardKind.Ward, CardKind.Sprint, CardKind.Mimic, CardKind.Seal, CardKind.Banish };
        state.Nodes = new()
        {
            Node("hq", "公司总部", "HQ", 65, 230, "有限供应：一次替换耗材可为隔离工具补充 1 次，费用 3；总部补封装不额外收现金，但占人、事务和工具次数。", headquarters: true),
            Node("hotel", "暮灯旅馆", "Hotel", 220, 100, "镜中住客：先观察、比较，再验证假说。合同 4；首次现场确认零钱 1，无调查奖励。"),
            Node("junction", "北街枢纽", "Hub", 410, 100, "北线双向枢纽；与南线在医院汇合。"),
            Node("hospital", "白榆医院", "Hospital", 520, 230, "一条必需规程；调查 2、回收 5。公开收容条件要求本轮照明。"),
            Node("source", "红箱源头", "Source", 735, 230, "调查 2、回收 8；确认归档规程后，使用独立源头专用箱，不消耗便携隔离工具。", source: true),
            Node("relay", "南街枢纽", "Hub", 220, 360, "南线双向枢纽，连接公开协作点。"),
            Node("depot", "公共协作点", "Cooperation", 410, 360, "开局公开签约；建设 4，次日起驻站营业。", cooperation: true)
        };
        state.Edges = new()
        {
            Edge("hq-hotel", "hq", "hotel"), Edge("hotel-junction", "hotel", "junction"),
            Edge("junction-hospital", "junction", "hospital"), Edge("hospital-source", "hospital", "source"),
            Edge("hq-relay", "hq", "relay"), Edge("relay-depot", "relay", "depot"), Edge("depot-hospital", "depot", "hospital")
        };
        foreach (var edge in state.Edges) edge.Name = $"{state.Node(edge.From).Name} ↔ {state.Node(edge.To).Name}";
        var mirror = new RuleState
        {
            Id = "hotel-mirror", NodeId = "hotel", Name = "镜中住客", Family = "MirrorGuest", Scope = RuleScope.Local,
            Trigger = RuleTrigger.MirrorSearch, MirrorVariantB = variantB, CorrectCandidate = variantB ? 1 : 0,
            Candidates = new() { "未照明的镜面房正常搜查会反噬。", "本次出勤同镜面房第二次及以后正常搜查会反噬，与灯光无关。", "黑暗中进入镜面房就会受伤。" },
            Notice = "镜面房内有两处有限容器。第一柜是寻物记录，第二处才是合同目标；另登记一份有限备用对照柜，原柜用后可委派采样。可从靠近入口的位置试探。",
            Clue = "同实例旧出勤录像：调查员在黑暗中进入镜面房，未观察到反噬；随后自述这是第二次搜查，出现反噬。第一次搜查的录像缺失。",
            Answer = variantB ? "本次出勤同镜面房第二次及以后正常搜查会反噬，与灯光无关。" : "未照明的镜面房正常搜查会反噬。",
            RoomIds = new() { "hotel-room1", "hotel-annex" }, WillDamage = 2, Wardable = true,
            ContrastSamplesRemaining = 1
        };
        state.Rules = new()
        {
            mirror,
            Rule("hospital-dark", "hospital", RuleTrigger.SearchInDark, "病历室规程", "公开收容要求：取样封装时必须照明。", "值班记录：亮灯后搜查、封装样本正常完成；摸黑搜查出现惊扰。", "不要在黑暗中搜查。", new() { "不要在黑暗中搜查。", "不要点灯。", "不要调查。" }),
            Rule("source-carry", "source", RuleTrigger.CarryUnsealed, "源头回收规程", "公开合同配置：仅源头专用箱能收容红箱，不能移用于其他物品。", "回收记录：未封装红箱携入背包会外溢；使用登记的源头专用箱完成收容后，外溢停止。", "异常物入包后应尽快封装。", new() { "异常物入包后应尽快封装。", "源头专用箱可封装任何物品。", "普通隔离工具可代替源头专用箱。" })
        };
        foreach (var node in state.Nodes.Where(n => n.Kind is "Hotel" or "Hospital" or "Source"))
        {
            node.RuleIds = state.Rules.Where(r => r.NodeId == node.Id).Select(r => r.Id).ToList();
            node.Rooms = Rooms(node.Id, node.Kind == "Hotel");
        }
        var hotel = state.Node("hotel"); hotel.SurveyReward = 0; hotel.RecoveryReward = 4;
        var mirrorRoom = hotel.Rooms.First(r => r.Id == "hotel-room1");
        mirrorRoom.Name = "镜面房"; mirrorRoom.IsMirrorRoom = true; mirrorRoom.HasArchiveCabinet = true; mirrorRoom.ArchiveSearchRemaining = 1;
        mirrorRoom.Description = "档案柜只给一次寻物记录，定位第二容器；第一次搜查不会直接发现或领取合同物。";
        var safeRoom = hotel.Rooms.First(r => r.Id == "hotel-room2");
        safeRoom.Name = "安全值班室"; safeRoom.AllowsKnock = false;
        safeRoom.Description = "不在镜中住客的作用房间内；先有一份真实便条库存可用于搜查参考，再有一件可选誊写护符夹。每份须分别搜查，不会刷新。";
        var annex = hotel.Rooms.First(r => r.Id == "hotel-annex");
        annex.Name = "未拼接的镜面侧房"; annex.IsMirrorRoom = true;
        annex.Description = $"沿用同一镜中住客实例；一盏唯一侧房灯，占 1 格，已封装出售报价固定为 {lampPrice}。放房、往返、搜查、封装仍须实际付费。";
        state.Node("source").RecoveryReward = 8;
        AddTarget(state, "hotel", "hotel-clock", "镜中住客的座钟", 2);
        AddTarget(state, "hospital", "hospital-vial", "回声样本", 1);
        state.Item("hospital-vial").RequiresLightForSealing = true;
        AddTarget(state, "source", "source-box", "源头红箱", 3);
        state.Items.Add(new ItemState { Id = "safe-note", Name = "值班便条", Kind = "Record", NodeId = "hotel", RoomId = "hotel-room2", Size = 1,
            Description = "安全房的一份真实库存，无现金奖励，无需封装；重访不刷新。" });
        state.Items.Add(new ItemState { Id = WardClipCatalog.Id, Name = WardClipCatalog.Name, Kind = WardClipCatalog.Kind,
            NodeId = "hotel", RoomId = "hotel-room2", Size = 1, Sealed = true, RequiresSealing = false, Value = 0,
            Charges = WardClipCatalog.MaximumCharges, MaxCharges = WardClipCatalog.MaximumCharges, Description = WardClipCatalog.Description });
        state.Items.Add(new ItemState { Id = "lamp", Name = "侧房灯", Kind = "Lamp", NodeId = "hotel", RoomId = "hotel-annex", RequiresSealing = true,
            Size = 1, Value = lampPrice, Description = $"唯一灯具，封装需隔离工具 1 次；公开固定报价 {lampPrice}。可出售、安装或保留未封装暂存，不能同时处置。" });
        state.Items.Add(new ItemState { Id = "seal-a", Name = "便携隔离工具", Kind = "SealKit", Location = ItemLocation.Warehouse, Charges = 2, MaxCharges = 2,
            Description = "唯一实物，初始与最大均 2 次；旅馆、医院和侧房灯各需 1 次。同一工具不能同时交给两队。" });
        state.Items.Add(new ItemState { Id = "source-case", Name = "源头专用箱", Kind = "SourceCase", ContractNodeId = "source", Location = ItemLocation.Warehouse,
            Charges = 1, MaxCharges = 1, Description = "开局公开的专用收容配置；源头规程归档确认后可配发，仅源头可用，最终随源头交付。" });
        state.Items.Add(new ItemState { Id = "supply-charge", Name = "供应商替换耗材", Kind = "SupplyCharge", Location = ItemLocation.Warehouse,
            Charges = 1, MaxCharges = 1, Description = "总部唯一一次补充供应；工程费用 3，1 人 / 1 事务，日终补 1 次、次日才可使用。" });
        state.Record($"M2 固定账例：七节点双向环路，五日期限，侧房灯公开报价 {lampPrice}。{(pressure ? "已加入独立传播压力，不能沿用无压力账例作为保证。" : "无交通附加费、无强制抢修或传播；三城生成尚未实现。")}");
        return state;
    }

    public static GameState CreateLegacy(int seed = 413)
    {
        var state = new GameState { Scenario = "Legacy", PressureEnabled = true, Seed = seed, RandomState = unchecked((ulong)(uint)seed) + 1 };
        state.Employees = Enumerable.Range(1, 6).Select(i => new Employee
        {
            Id = $"e{i}", Name = new[] { "林舟", "陈墨", "许灯", "陆宁", "沈青", "顾川" }[i - 1]
        }).ToList();
        state.TeamDeck = new() { CardKind.Investigate, CardKind.Investigate, CardKind.Search, CardKind.Search,
            CardKind.Knock, CardKind.Light, CardKind.Rest, CardKind.Ward, CardKind.Sprint, CardKind.Mimic,
            CardKind.Seal, CardKind.Banish };
        state.Nodes = new()
        {
            Node("hq", "公司总部", "HQ", 85, 210, "人员、资金与归档的起点。", headquarters: true),
            Node("hotel", "暮灯旅馆", "Hotel", 240, 100, "回收停摆座钟并确认必要规程后，可投入 4 资金接管。"),
            Node("hospital", "白榆医院", "Hospital", 240, 320, "轻量医院现场；样本回收与调查合同。"),
            Node("junction", "北街枢纽", "Hub", 430, 75, "双向通行枢纽，环路可绕行。"),
            Node("depot", "旧城合作站", "Cooperation", 415, 230, "公开合作点：无需异常物；支付 4 资金、安排 1 人施工即可接管。", cooperation: true),
            Node("relay", "南线中继", "Hub", 460, 375, "线路留有封装运输告示；修路不会解除守则。"),
            Node("source", "红箱源头", "Source", 650, 210, "关键回收可按已确认规程委派；源头每两日预告一次扩张。", source: true)
        };
        state.Edges = new()
        {
            Edge("hq-hotel", "hq", "hotel"), Edge("hq-hospital", "hq", "hospital"),
            Edge("hotel-junction", "hotel", "junction"), Edge("junction-source", "junction", "source"),
            Edge("source-relay", "source", "relay"), Edge("hospital-relay", "hospital", "relay", "line-seal"),
            Edge("hotel-depot", "hotel", "depot"), Edge("hospital-depot", "hospital", "depot")
        };
        state.Rules = new()
        {
            Rule("hotel-dark", "hotel", RuleTrigger.SearchInDark, "暗处搜查", "登记簿提醒：检查灯光后再整理遗物。", "值班记录：亮灯整理物品时无人受伤，熄灯搜查时调查员受到惊扰。", "不要在黑暗中搜查。", new() { "不要在黑暗中搜查。", "不要在亮灯时搜查。", "不要在入口休息。" }),
            Rule("hotel-knock", "hotel", RuleTrigger.Knock, "门内应答", "某扇门内总是传来第三次敲击。", "旧住客笔记：不敲门的人安然无恙，主动敲门的人受到了回应。", "不要主动敲门。", new() { "不要主动敲门。", "不要搜查。", "不要点灯。" }),
            Rule("hospital-dark", "hospital", RuleTrigger.SearchInDark, "暗室病历", "病历柜边挂着一盏完好的灯。", "交班单：取病历前点亮灯，夜间摸黑取物的人遭到惊扰。", "不要在黑暗中搜查。", new() { "不要在黑暗中搜查。", "不要在亮灯时移动。", "不要在入口调查。" }),
            Rule("hospital-rest", "hospital", RuleTrigger.Rest, "值班禁眠", "休息椅上有警告：保持清醒。", "观察记录：坐下休息后异动增加，正常走动未见同一反应。", "不要在医院休息。", new() { "不要在医院休息。", "不要在医院点灯。", "不要在医院调查。" }),
            Rule("source-carry", "source", RuleTrigger.CarryUnsealed, "红箱外溢", "红箱旁的铅封完好，地面有拖拽留下的深痕。", "回收日志：未封装的异常物进入背包时出现外溢；完成封装后外溢停止。", "异常物入包后应尽快封装。", new() { "异常物入包后应尽快封装。", "只要红色物品入包就必然丢失。", "封装会使外溢更强。" }),
            Rule("line-seal", "relay", RuleTrigger.TransportUnsealed, "南线运输守则", "线路告示缺失了一部分，运输仍可能带来未知后果。", "运输日志：未封装异常物经过南线，返程到达节点留下侵入；封装运输没有这一记录。", "经过南线前封装异常物。", new() { "经过南线前封装异常物。", "不要运输已封装物品。", "空手经过南线也必然侵入。" }, RuleScope.Line)
        };
        foreach (var node in state.Nodes.Where(n => n.Kind is "Hotel" or "Hospital" or "Source"))
        {
            node.RuleIds = state.Rules.Where(r => r.NodeId == node.Id && r.Scope == RuleScope.Local).Select(r => r.Id).ToList();
            node.Rooms = Rooms(node.Id, node.Kind == "Hotel");
        }
        state.Node("relay").RuleIds.Add("line-seal");
        state.Node("source").RecoveryReward = 8;
        AddTarget(state, "hotel", "hotel-clock", "停摆座钟", 2);
        AddTarget(state, "hospital", "hospital-vial", "回声样本", 1);
        AddTarget(state, "source", "source-box", "源头红箱", 3);
        state.Items.Add(new ItemState { Id = "hotel-token", Name = "玻璃门牌", NodeId = "hotel", RoomId = "hotel-room2", Size = 1, Value = 2, Description = "可选异常物；撤离后作为实物进入仓库。" });
        state.Items.Add(new ItemState { Id = "reveal-lens", Name = "残页透镜", NodeId = "hotel", RoomId = "hotel-annex", Kind = "RevealLens", Size = 1, Charges = 1, Description = "可选稀有工具；取得后消耗 1 次揭晓当前地点一条未确认规则，不发普通确认奖励。" });
        foreach (var id in new[] { "seal-a", "seal-b" })
            state.Items.Add(new ItemState { Id = id, Name = id == "seal-a" ? "封装工具 A" : "封装工具 B", Kind = "SealKit", Location = ItemLocation.Warehouse, Charges = 2, Description = "独立实例，各有 2 次委派封装额度；锁定时预留，不能分给两队。" });
        state.Record("单城验证场景：固定七节点、五日期限；种子只控制现场抽牌，不代表随机三城生成。目标：处置源头并让 2 个服务点在扩张后有效营业。");
        return state;
    }

    private static CityNode Node(string id, string name, string kind, float x, float y, string description,
        bool headquarters = false, bool source = false, bool cooperation = false) => new()
        { Id = id, Name = name, Kind = kind, X = x / 735f, Y = y / 460f, Description = description, IsHeadquarters = headquarters,
            IsSource = source, PublicCooperation = cooperation, SurveyReward = kind is "Hotel" or "Hospital" or "Source" ? 2 : 0 };

    private static CityEdge Edge(string id, string from, string to, string? ruleId = null) => new()
        { Id = id, From = from, To = to, Name = $"{from} ↔ {to}", RuleId = ruleId };

    private static RuleState Rule(string id, string node, RuleTrigger trigger, string name, string notice,
        string clue, string answer, List<string> candidates, RuleScope scope = RuleScope.Local)
    {
        int correct = trigger switch { RuleTrigger.Knock => 1, RuleTrigger.Rest => 2, RuleTrigger.CarryUnsealed => 2, RuleTrigger.TransportUnsealed => 1, _ => 0 };
        (candidates[0], candidates[correct]) = (candidates[correct], candidates[0]);
        return new RuleState { Id = id, NodeId = node, Scope = scope, Trigger = trigger, Name = name, Notice = notice,
            Clue = clue, Answer = answer, Candidates = candidates, CorrectCandidate = correct,
            WillDamage = trigger == RuleTrigger.Rest ? 0 : 2, DisturbanceGain = trigger == RuleTrigger.Rest ? 2 : 0 };
    }

    private static List<RoomState> Rooms(string node, bool hotel)
    {
        var rooms = new List<RoomState>
        {
        new() { Id = $"{node}-entry", Name = "入口 / 接待处", IsEntrance = true, X = 0, Y = 0, Entered = false,
            Description = "基础撤离出口；可在此调查可靠记录。", Neighbors = new() { $"{node}-room1" } },
        new() { Id = $"{node}-room1", Name = hotel ? "旧客房" : "回收室", X = 1, Y = 0,
            Description = "委托所指的回收地点；物品未入包前仍属于本房间库存。", Neighbors = new() { $"{node}-entry", $"{node}-room2" } },
        new() { Id = $"{node}-room2", Name = hotel ? "镜厅" : "档案室", X = 2, Y = 0, AllowsKnock = hotel,
            Description = "可选支路；已知通路可双向返回。", Neighbors = new() { $"{node}-room1" } }
        };
        if (hotel)
        {
            rooms[1].Neighbors.Add($"{node}-annex");
            rooms.Add(new RoomState { Id = $"{node}-annex", Name = "未拼接的侧房", X = 1, Y = 1, Placed = false,
                Description = "可选房间，放置需 1 行动与 1 墨水；内部保留一件可取回的稀有工具。", Neighbors = new() { $"{node}-room1" } });
        }
        return rooms;
    }

    private static void AddTarget(GameState state, string nodeId, string itemId, string name, int size)
    {
        state.Node(nodeId).TargetItemId = itemId;
        state.Items.Add(new ItemState { Id = itemId, Name = name, NodeId = nodeId, RoomId = $"{nodeId}-room1",
            ContractNodeId = nodeId, Size = size, Required = true, RequiresSealing = true,
            Description = "唯一合同实物；完成封装并成功撤离后才能交付。" });
    }
}
