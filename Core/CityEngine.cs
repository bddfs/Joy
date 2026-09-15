using System;
using System.Collections.Generic;
using System.Linq;

namespace Joy.Core;

public sealed record CityBudget(int CurrentMoney, int AlreadyPaid, int UnpaidUpfront, int LockedOperatingIncome,
    int ConditionalIncome, int DueExpenses, int ConservativeBalance, int ConditionalBalance, string Text);

/// <summary>Deterministic city transactions for the explicitly scoped single-city prototype.</summary>
public sealed class CityEngine
{
    private readonly GameState state;
    public CityEngine(GameState state) => this.state = state;

    public CityBudget GetBudget()
    {
        bool planning = state.Phase == DayPhase.Preparation;
        var tasks = planning ? state.Plan : state.LockedPlan;
        int paid = -state.Ledger.Where(l => l.Day == state.Day && l.Category == "预付").Sum(l => l.Amount);
        int upfront = planning ? tasks.Where(t => !t.Paid).Sum(t => t.Cost) : 0;
        var incomeNodes = planning ? OperatingNodeIds() : state.MorningOperatingNodes;
        int income = incomeNodes.Count(id => !state.PaidRewards.Contains($"income:{state.Day}:{id}"));
        int expense = state.Ledger.Any(l => l.Day == state.Day && l.Category == "基本支出") ? 0 : state.DailyExpense;
        int conditional = 0;
        var rewardKeys = new HashSet<string>();
        foreach (var task in tasks)
        {
            var node = state.Node(task.NodeId);
            if (task.Kind == JobKind.Recovery && !node.RecoveryPaid && rewardKeys.Add($"recovery:{node.Id}")) conditional += node.RecoveryReward;
            if (task.Kind == JobKind.Survey && !node.SurveyPaid && rewardKeys.Add($"survey:{node.Id}")) conditional += node.SurveyReward;
        }
        foreach (var item in state.Items.Where(i => i.Required && i.Sealed && i.Location == ItemLocation.Warehouse))
        {
            var node = state.Nodes.FirstOrDefault(n => n.TargetItemId == item.Id);
            if (node is not null && !node.RecoveryPaid && rewardKeys.Add($"recovery:{node.Id}")) conditional += node.RecoveryReward;
        }
        conditional += state.Items.Where(i => i.Location == ItemLocation.Warehouse && i.Disposition == "Sell").Sum(i => i.Value);
        if (state.Field is { Finished: false } field) conditional += field.Coins;
        int conservative = state.Money - upfront + income - expense;
        string text = $"当前可用 {state.Money} · 本日预付已付 {paid}（不再重复扣）\n尚未支付计划 {upfront} · 晨间营业尚未到账 {income} · 日终待付 {expense}\n保守余额 {conservative} · 条件收入 {conditional} · 条件满足后 {conservative + conditional}\n未完成合同、未撤出零钱与未知后果不作为保证收入。";
        return new(state.Money, paid, upfront, income, conditional, expense, conservative, conservative + conditional, text);
    }

    public ActionResult ScheduleSupply(string toolId = "seal-a") => AddTask(new Assignment
        { Kind = JobKind.Supply, NodeId = HeadquartersId(), TargetItemId = toolId });
    public ActionResult ScheduleHeadquartersSeal(string itemId) => AddTask(new Assignment
        { Kind = JobKind.HeadquartersSeal, NodeId = HeadquartersId(), TargetItemId = itemId });
    public ActionResult ScheduleResearch(string itemId) => AddTask(new Assignment
        { Kind = JobKind.Research, NodeId = HeadquartersId(), TargetItemId = itemId });

    public ActionResult SellItem(string itemId)
    {
        if (state.Phase is not (DayPhase.Preparation or DayPhase.Executing)) return ActionResult.Fail("本阶段不能追加旧城处置。");
        var item = state.Items.FirstOrDefault(i => i.Id == itemId);
        if (item is null || item.Kind != "Lamp" || item.Location != ItemLocation.Warehouse || !item.Sealed ||
            (state.Phase == DayPhase.Preparation && item.AvailableDay > state.Day) || item.ReservedBy is not null || PlannedItem(item.Id))
            return ActionResult.Fail("出售需要已封装、成功归库且未被任务预留的灯具；未封装或已安装不能出售。");
        if (item.Disposition == "Sell") return ActionResult.Fail("该唯一灯具已登记出售，不会重复付款。");
        item.Disposition = "Sell";
        state.Record($"已登记日终出售：{item.Name}，公开报价 {item.Value}；完成处置前未入账。");
        return ActionResult.Ok($"已登记 {item.Name} 日终出售，报价 {item.Value}。");
    }

    public ActionResult KeepItem(string itemId)
    {
        if (state.Phase is not (DayPhase.Preparation or DayPhase.Executing)) return ActionResult.Fail("本阶段不能调整处置。");
        var item = state.Items.FirstOrDefault(i => i.Id == itemId);
        if (item is null || item.Location != ItemLocation.Warehouse || item.Disposition != "Sell") return ActionResult.Fail("没有可撤销的待出售实物。");
        item.Disposition = "";
        return ActionResult.Ok($"已保留 {item.Name}，不会出售。");
    }

    public ActionResult InstallLamp(string itemId, string nodeId)
    {
        if (state.Phase != DayPhase.Preparation) return ActionResult.Fail("成功撤出后，下一准备阶段才可安装。");
        var item = state.Items.FirstOrDefault(i => i.Id == itemId);
        if (item is null || item.Kind != "Lamp" || item.Location != ItemLocation.Warehouse || !item.Sealed || item.AvailableDay > state.Day ||
            item.ReservedBy is not null || item.Disposition != "" || PlannedItem(itemId)) return ActionResult.Fail("安装需要可用、已封装且未预留或登记出售的仓库灯具。");
        if (!OperatingNodeIds().Contains(nodeId)) return ActionResult.Fail("请先为已生效、连通且维护正常的服务点安排驻站。");
        item.Location = ItemLocation.Installed;
        item.InstalledNodeId = nodeId;
        item.OwnerId = "";
        state.Record($"{item.Name} 安装于 {state.Node(nodeId).Name}；不另收费，从本日锁定生效，每日最多支援一队一次。");
        return ActionResult.Ok("灯具已安装；需分配给本日的一项出勤，锁定后才能使用。");
    }

    public ActionResult UninstallLamp(string itemId)
    {
        if (state.Phase != DayPhase.Preparation) return ActionResult.Fail("只能在下一准备阶段拆卸灯具。");
        var item = state.Items.FirstOrDefault(i => i.Id == itemId);
        if (item is null || item.Location != ItemLocation.Installed || item.ReservedBy is not null || PlannedItem(itemId)) return ActionResult.Fail("灯具未安装或已分配给本日任务，请先取消支援分配。");
        item.Location = ItemLocation.Warehouse;
        item.InstalledNodeId = "";
        return ActionResult.Ok("灯具已拆回仓库；该实例本日已用额度仍保留。");
    }

    public ActionResult AssignSupport(string assignmentId, string? lampId)
    {
        if (state.Phase != DayPhase.Preparation) return ActionResult.Fail("任务锁定后不能更换支援。");
        var task = state.Plan.FirstOrDefault(t => t.Id == assignmentId);
        if (task is null || !IsField(task.Kind)) return ActionResult.Fail("请选择一项已安排的出勤。");
        string previous = task.SupportItemId;
        task.SupportItemId = lampId ?? "";
        var valid = ValidatePlan(state.Plan);
        if (!valid.Success) { task.SupportItemId = previous; return valid; }
        return ActionResult.Ok(lampId is null ? "已取消本次网点支援。" : "已分配灯具支援；同一实例本日只能供一队使用一次。");
    }

    public ActionResult SetTaskTools(string assignmentId, IEnumerable<string> toolIds)
    {
        if (state.Phase != DayPhase.Preparation) return ActionResult.Fail("任务已经锁定，不能更换或释放本日配装。");
        var task = state.Plan.FirstOrDefault(t => t.Id == assignmentId);
        if (task is null || task.Kind != JobKind.Manual) return ActionResult.Fail("只有准备阶段已安排的手操出勤可以调整配装。");
        var previous = task.ToolIds;
        task.ToolIds = toolIds.ToList();
        var valid = ValidatePlan(state.Plan);
        if (!valid.Success) { task.ToolIds = previous; return valid; }
        return ActionResult.Ok(task.ToolIds.Count == 0 ? "该队已改为空配装；可以空手调查或重返取回遗落物。" : "手操配装已更新，使用真实工具实例和当前剩余次数。");
    }

    public ActionResult CanUseLampSupport(Assignment task)
    {
        var lamp = state.Items.FirstOrDefault(i => i.Id == task.SupportItemId);
        if (state.Phase != DayPhase.Executing || lamp is null || lamp.Location != ItemLocation.Installed ||
            lamp.ReservedBy != task.Id || lamp.SupportUsedDay == state.Day || !OperatingNodeIds().Contains(lamp.InstalledNodeId))
            return ActionResult.Fail("灯具支援未锁定、今日已用，或提供支援的网点已停业。");
        return ActionResult.Ok();
    }

    public ActionPreview PreviewTask(JobKind kind, string nodeId) => PreviewTask(Prepare(new Assignment { Kind = kind, NodeId = nodeId }));

    public ActionPreview PreviewTask(Assignment assignment)
    {
        var prepared = Prepare(assignment);
        var validation = ValidatePlan(state.Plan.Append(prepared).ToList());
        if (!validation.Success) return new(false, prepared.Cost, validation.Message);
        var node = state.Node(prepared.NodeId);
        string outcome = prepared.Kind switch
        {
            JobKind.Survey => "日终归档一条尚未取得的可靠线索；首次调查里程碑支付 " + node.SurveyReward + "。仍须根据证据判断候选。",
            JobKind.ContrastSurvey => "安全对照模板：本次首次在暗处正常搜查，使用公开登记的有限样本，携 1 护符抵挡最多 2 心志反噬；模板防护足够，无恢复缺口。不领合同物，不消耗隔离次数；日终只归档实际观察，次日判断。",
            JobKind.Recovery => "按提交时已确认规程回收、封装、运输并交付唯一实物；合同支付 " + node.RecoveryReward + $"。使用{(state.Scenario == "M2" && node.IsSource ? "源头专用箱，不消耗便携隔离次数" : "已预留隔离工具 1 次")}。",
            JobKind.Manual => "手操现场；只有成功撤离的零钱和实物可带回，失败人员次日恢复。未知守则仍可能生效。",
            JobKind.Build => "支付 4 资金接管；本日无营业收入或封控，次日起可驻站营业。",
            JobKind.Maintenance => "按已确认规程清除现有入侵、恢复维护；保留设施，不重复建设。",
            JobKind.Repair => "抢修目标节点相邻的已中断通路；保留线路守则，旧点无补发收入。",
            JobKind.Recruit => "扩编至 8 人；日终加入员工，次日起基本支出增加 1。",
            JobKind.Supply => "费用 3；消耗供应商唯一替换耗材，为指定工具补 1 次，最大 2 次；日终完成，次日可用，本日不能借用预期余量。",
            JobKind.HeadquartersSeal => "无额外现金费；预留并消耗隔离工具 1 次，日终完成封装，次日准备才可出售或安装。",
            JobKind.Research => "施工·研究，占 1 人 / 1 事务；消耗已封装可选实物并记研究档案，不另给未声明现金或能力。",
            _ => ""
        };
        int minimumBalance = state.Money - state.Plan.Sum(p => p.Cost) - prepared.Cost - state.DailyExpense;
        return new(true, prepared.Cost, $"预付 {prepared.Cost} · 占 {prepared.EmployeeIds.Count} 人 / 1 事务。{outcome} 最坏情况下扣除基本支出后余额 {minimumBalance}（未计当日新收益）。");
    }

    public ActionResult AddTask(JobKind kind, string nodeId, IEnumerable<string>? employees = null, IEnumerable<string>? tools = null)
        => AddTask(new Assignment { Kind = kind, NodeId = nodeId, EmployeeIds = employees?.ToList() ?? new(), ToolIds = tools?.ToList() ?? new() });

    public ActionResult AddTask(Assignment assignment)
    {
        if (state.Phase != DayPhase.Preparation) return ActionResult.Fail("任务已锁定，本日不能重排。");
        var prepared = Prepare(assignment);
        var validation = ValidatePlan(state.Plan.Append(prepared).ToList());
        if (!validation.Success) return validation;
        state.Plan.Add(prepared);
        return ActionResult.Ok($"已安排 {JobName(prepared.Kind)}：{state.Node(prepared.NodeId).Name}；准备阶段尚未扣费。");
    }

    public ActionResult RemoveTask(string id)
    {
        if (state.Phase != DayPhase.Preparation) return ActionResult.Fail("锁定后不可撤回安排。");
        int removed = state.Plan.RemoveAll(p => p.Id == id);
        return removed > 0 ? ActionResult.Ok("已移除待提交安排。") : ActionResult.Fail("未找到该待提交任务。");
    }

    public ActionResult AssignStation(string nodeId, string? employeeId)
    {
        if (state.Phase != DayPhase.Preparation) return ActionResult.Fail("岗位已锁定。");
        var node = state.Nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node is null || !node.Built || node.IsHeadquarters) return ActionResult.Fail("只有已建成服务点能安排驻站。");
        if (string.IsNullOrWhiteSpace(employeeId))
        {
            state.Stations.Remove(nodeId);
            return ActionResult.Ok("已撤回驻站安排。");
        }
        var employee = state.Employees.FirstOrDefault(e => e.Id == employeeId);
        if (employee is null) return ActionResult.Fail("员工不存在。");
        if (Recovering(employee)) return ActionResult.Fail($"{employee.Name} 正在恢复，本日不能工作。");
        if (state.Stations.Any(s => s.Key != nodeId && s.Value == employeeId) || state.Plan.Any(p => p.EmployeeIds.Contains(employeeId)))
            return ActionResult.Fail($"员工冲突：{employee.Name} 已被另一岗位或任务占用。");
        state.Stations[nodeId] = employeeId;
        return ActionResult.Ok($"{employee.Name} 将驻站 {node.Name}。");
    }

    public ActionResult SetBlockade(string nodeId, string? edgeId)
    {
        if (state.Phase != DayPhase.Preparation) return ActionResult.Fail("锁定后不能调整封控。");
        var node = state.Nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node is null || !node.Built) return ActionResult.Fail("封控需要已建立的服务点。");
        if (edgeId is not null && !state.Edges.Any(e => e.Id == edgeId && (e.From == nodeId || e.To == nodeId)))
            return ActionResult.Fail("每个服务点只能封控一条相邻边。");
        node.BlockadeEdgeId = edgeId;
        return ActionResult.Ok(edgeId is null ? "已取消封控。" : "已选择当晚封控边；仍须驻站、连通与已知规程。");
    }

    public ActionResult ConfirmRule(string ruleId, int candidate)
    {
        if (state.Phase != DayPhase.Preparation) return ActionResult.Fail("城市判断只在准备阶段开放。");
        var rule = state.Rules.FirstOrDefault(r => r.Id == ruleId);
        if (rule is null) return ActionResult.Fail("不存在该规则实例。");
        if (rule.Confirmed) return ActionResult.Fail("该规则已确认，不重复确认或领奖。");
        if (!rule.Archived || (!rule.EvidenceFound && !rule.Observed)) return ActionResult.Fail("尚无已归档的可靠线索或实际观察；现场记录在日终归档。");
        if (candidate < 0 || candidate >= rule.Candidates.Count) return ActionResult.Fail("候选不存在。");
        if (rule.WrongCandidates.Contains(candidate)) return ActionResult.Fail("这个候选已有错误记录，不能跨模式重复提交。");
        if (rule.LastCityAttemptDay == state.Day) return ActionResult.Fail("本规则今天已提交过候选，次日才可再判断。");
        rule.LastCityAttemptDay = state.Day;
        if (candidate != rule.CorrectCandidate)
        {
            if (!rule.WrongCandidates.Contains(candidate)) rule.WrongCandidates.Add(candidate);
            state.Record($"候选被证据否定：{rule.Candidates[candidate]}；错误记录保留。");
            return ActionResult.Ok("判断错误，记录已保留；今天不能再次判断这条规则。");
        }
        rule.Confirmed = true;
        rule.ConfirmationRewardClaimed = true; // City confirmation shares the reward ledger without granting field cash.
        state.Record($"城市确认规程：{rule.Answer}；不发现场心志或零钱。");
        return ActionResult.Ok("规程已确认，后续任务可按该条件受理。");
    }

    public ActionResult LockDay()
    {
        if (state.Phase != DayPhase.Preparation) return ActionResult.Fail("当天已经提交，不会再次预付。");
        var validation = ValidatePlan(state.Plan);
        if (!validation.Success) return validation;
        state.MorningOperatingNodes = OperatingNodeIds().ToList();
        state.LastDaySummary.Clear();
        state.Phase = DayPhase.Locked;
        state.LockedPlan = state.Plan.Select(Clone).ToList();
        foreach (var employee in state.Employees) employee.Status = Recovering(employee) ? EmployeeStatus.Recovery : EmployeeStatus.Standby;
        foreach (var station in state.Stations) state.Employees.First(e => e.Id == station.Value).Status = EmployeeStatus.Stationed;
        foreach (var task in state.LockedPlan)
        {
            task.KnownRuleIds = state.Rules.Where(r => r.Confirmed && r.Archived).Select(r => r.Id).ToList();
            Pay(-task.Cost, "预付", $"{JobName(task.Kind)} · {state.Node(task.NodeId).Name}");
            task.Paid = true;
            foreach (var id in task.EmployeeIds)
                state.Employees.First(e => e.Id == id).Status = IsField(task.Kind) ? EmployeeStatus.Deployed : EmployeeStatus.Construction;
            foreach (var id in task.ToolIds) state.Item(id).ReservedBy = task.Id;
            if (!string.IsNullOrEmpty(task.TargetItemId)) state.Item(task.TargetItemId).ReservedBy = task.Id;
            if (!string.IsNullOrEmpty(task.SupportItemId)) state.Item(task.SupportItemId).ReservedBy = task.Id;
            if (task.Kind is JobKind.Recovery or JobKind.Manual)
            {
                var target = Target(state.Node(task.NodeId));
                if (target is not null && target.Location == ItemLocation.Room) target.ReservedBy = task.Id;
            }
        }
        state.Phase = DayPhase.Executing;
        state.LastDaySummary.Add($"准备预付：{state.LockedPlan.Sum(t => t.Cost)}；本日任务 {state.LockedPlan.Count}/{state.TransactionLimit}，驻站 {state.Stations.Count} 人。");
        state.Record("当天任务与驻站已锁定并预付；手操提前撤离不会释放当日人员或事务位。");
        state.LastCheckpoint = $"第 {state.Day} 日 · 已锁定安排";
        return ActionResult.Ok("已锁定并预付。完成手操后统一日结；纯委派可直接执行日结。");
    }

    public ActionResult CompleteManual()
    {
        if (state.Phase != DayPhase.Executing) return ActionResult.Fail("当前不在执行阶段。");
        var task = state.LockedPlan.FirstOrDefault(p => p.Kind == JobKind.Manual && !p.Completed);
        if (task is null) return ActionResult.Ok("没有待结转的手操任务。");
        var field = state.Field;
        if (field is null || field.AssignmentId != task.Id || !field.Finished) return ActionResult.Fail("手操尚未结束，必须撤离或完成失败结算。");
        if (field.Escaped)
        {
            var returning = state.Items.Where(i => i.Location == ItemLocation.Backpack && i.OwnerId == task.Id).ToList();
            ApplyTransport(returning, task.Route.AsEnumerable().Reverse().ToList());
            foreach (var item in returning) { item.Location = ItemLocation.Warehouse; item.OwnerId = ""; item.ReservedBy = null; }
            if (returning.Count > 0) state.LastDaySummary.Add("成功带回：" + string.Join("、", returning.Select(i => i.Name + (i.RequiresSealing && !i.Sealed ? "（未封装暂存）" : ""))) + "；实物交付与处置尚未完成。");
            if (field.Coins > 0) Pay(field.Coins, "现场零钱", $"{state.Node(task.NodeId).Name} 成功撤离");
            task.Successful = true;
        }
        else
        {
            foreach (var item in state.Items.Where(i => i.Location == ItemLocation.Backpack && i.OwnerId == task.Id).ToList())
            {
                item.OwnerId = "";
                item.ReservedBy = null;
                if (state.Scenario == "Legacy" && field.BroughtItemIds.Contains(item.Id)) item.Location = ItemLocation.Warehouse;
                else { item.Location = ItemLocation.Room; item.NodeId = field.NodeId; item.RoomId = field.RoomId; item.Discovered = true; }
            }
            foreach (var id in task.EmployeeIds)
            {
                var employee = state.Employees.First(e => e.Id == id);
                employee.RecoverUntilDay = state.Day + 2;
                employee.Status = EmployeeStatus.Recovery;
            }
            state.Record("出勤失败：本次携出收益不入账，合同物保留唯一遗落记录；员工下一经营日恢复。");
            state.LastDaySummary.Add("出勤失败：未撤出零钱丢失；全部携带实物按唯一位置遗落，已用工具次数不退。队员恢复一经营日，可重返取回。");
        }
        task.Completed = true;
        ReleaseReservations(task);
        state.LastCheckpoint = $"第 {state.Day} 日 · 出勤已结转，等待日结";
        return ActionResult.Ok("手操作业已结转；调查、合同与工程统一在日终归档结算。");
    }

    public ActionResult SettleDay()
    {
        if (state.Phase != DayPhase.Executing) return ActionResult.Fail("只能对已锁定且尚未结算的经营日执行一次日结。");
        var manualResult = CompleteManual();
        if (!manualResult.Success) return manualResult;
        state.Phase = DayPhase.Settlement;
        int expenseToday = state.DailyExpense;
        // All acceptance criteria were fixed at lock; completed manual intel never upgrades another assignment.
        foreach (var task in state.LockedPlan.Where(p => !p.Completed && IsField(p.Kind))) ExecuteDelegated(task);
        foreach (var rule in state.Rules.Where(r => r.EvidenceFound || r.Observed || r.Confirmed))
        {
            rule.Archived = true;
            if (rule.HasContrastObservation) rule.ArchivedContrastObservation = true;
        }
        foreach (var task in state.LockedPlan.Where(p => p.Kind is JobKind.Survey or JobKind.Manual && p.Successful))
            SettleSurvey(state.Node(task.NodeId));
        foreach (var item in state.Items.Where(i => i.Location == ItemLocation.Warehouse && i.Required && i.Sealed).ToList()) Deliver(item);
        foreach (var task in state.LockedPlan.Where(p => !p.Completed)) ExecuteDelegated(task);
        foreach (var item in state.Items.Where(i => i.Location == ItemLocation.Warehouse && i.Sealed && i.Disposition == "Sell").ToList())
        {
            if (RewardOnce($"sale:{item.Id}", item.Value, "可选物出售", item.Name))
            {
                item.Location = ItemLocation.Sold;
                item.Disposition = "";
                state.LastDaySummary.Add($"出售 {item.Name}：到账 {item.Value}，唯一实例已离开库存。");
            }
        }
        foreach (var nodeId in state.MorningOperatingNodes)
            RewardOnce($"income:{state.Day}:{nodeId}", 1, "营业", state.Node(nodeId).Name);
        bool unpaid = state.Money < expenseToday;
        if (!unpaid) { Pay(-expenseToday, "基本支出", "包含常规驻站；不重复按员工扣工资"); state.LastDaySummary.Add($"营业按晨间 {state.MorningOperatingNodes.Count} 点计入；支付基本经营支出 {expenseToday}，驻站不重复计薪。"); }
        else state.Record($"经营失败：必要支出 {expenseToday}，可用资金 {state.Money}；先判断必要支出支付，再判断交接。");
        ApplySpread();
        int active = OperatingNodeIds().Count;
        state.LastDaySummary.Add($"扩张后有效营业 {active}/{state.RequiredStations}；日终余额 {state.Money}。");
        foreach (var task in state.LockedPlan) ReleaseReservations(task);
        if (unpaid) { state.Phase = DayPhase.Failed; state.Outcome = "必要经营支出不足。"; }
        else if (state.SourceRecovered && active >= state.RequiredStations)
        {
            state.Phase = DayPhase.Victory;
            state.Outcome = $"单城验证完成：源头已处置，扩张后有效营业 {active} 点；第 {state.Day} 日，余额 {state.Money}。";
            foreach (var lamp in state.Items.Where(i => i.Location == ItemLocation.Installed))
            {
                lamp.OwnerId = "当地分公司";
                state.LastDaySummary.Add($"已安装的 {lamp.Name} 留给 {state.Node(lamp.InstalledNodeId).Name}，不复制到总部库存。");
            }
        }
        else if (state.Day >= state.Deadline)
        {
            state.Phase = DayPhase.Failed;
            state.Outcome = $"期限已到：源头{(state.SourceRecovered ? "已" : "未")}处置，有效营业 {active}/{state.RequiredStations}。";
        }
        else
        {
            var newAbilities = state.LockedPlan.Where(t => t.Successful && t.Kind is JobKind.Build or JobKind.Supply or JobKind.HeadquartersSeal)
                .Select(t => t.Kind == JobKind.Build ? state.Node(t.NodeId).Name + "可安排驻站" : state.Item(t.TargetItemId).Name + "已完成工程并可使用").ToList();
            state.Day++;
            state.Phase = DayPhase.Preparation;
            state.Plan.Clear();
            state.LockedPlan.Clear();
            state.Field = null;
            state.MorningOperatingNodes.Clear();
            foreach (var employee in state.Employees)
                employee.Status = Recovering(employee) ? EmployeeStatus.Recovery : state.Stations.ContainsValue(employee.Id) ? EmployeeStatus.Stationed : EmployeeStatus.Standby;
            GenerateForecast();
            if (newAbilities.Count > 0) state.LastDaySummary.Add("今日新增可用：" + string.Join("、", newAbilities) + "。");
        }
        state.LastCheckpoint = state.Phase is DayPhase.Victory or DayPhase.Failed ? state.Outcome : $"第 {state.Day} 日 · 准备阶段";
        state.Record(state.Phase is DayPhase.Victory or DayPhase.Failed ? state.Outcome : "统一日结完成，情报已归档，进入下一日准备。");
        return ActionResult.Ok(state.LastCheckpoint);
    }

    public ActionResult EmergencySupport()
    {
        if (state.Phase != DayPhase.Preparation) return ActionResult.Fail("紧急支援只能在准备阶段使用。");
        if (state.EmergencySupportUsed) return ActionResult.Fail("本次验证远征的紧急支援已经领取。");
        state.EmergencySupportUsed = true;
        Pay(6, "紧急支援", "一次性暂定缓冲；记录为使用过支援，不实现最终评价公式");
        return ActionResult.Ok("获得 6 资金；一次性支援已记入本局记录。");
    }

    public List<string> FindRoute(string nodeId)
    {
        if (!state.Nodes.Any(n => n.Id == nodeId)) return new();
        var headquarters = state.Nodes.FirstOrDefault(n => n.IsHeadquarters);
        if (headquarters is null) return new();
        var queue = new Queue<List<string>>();
        var visited = new HashSet<string> { headquarters.Id };
        queue.Enqueue(new() { headquarters.Id });
        while (queue.Count > 0)
        {
            var route = queue.Dequeue();
            string current = route[^1];
            if (current == nodeId) return route;
            foreach (var edge in state.Edges.Where(e => e.Passable && (e.From == current || e.To == current)))
            {
                string next = edge.From == current ? edge.To : edge.From;
                if (visited.Add(next)) queue.Enqueue(route.Append(next).ToList());
            }
        }
        return new();
    }

    public List<string> OperatingNodeIds() => state.Nodes.Where(n => n.Built && n.BuiltDay < state.Day &&
        !n.IsHeadquarters && !n.Invaded && n.Maintained && state.Stations.ContainsKey(n.Id) && FindRoute(n.Id).Count > 0)
        .Select(n => n.Id).ToList();

    public ActionPreview PreviewTransport(IEnumerable<string> itemIds, IEnumerable<string> route)
    {
        var ids = itemIds.ToList();
        var path = route.ToList();
        if (!ValidPath(path)) return new(false, 0, "运输路径不存在或已有公开中断。");
        if (ids.Any(id => !state.Items.Any(i => i.Id == id))) return new(false, 0, "运输清单含不存在的实物。");
        var items = ids.Select(state.Item).ToList();
        var knownTargets = new List<string>();
        bool unknown = false;
        foreach (var step in PathEdges(path))
        {
            if (step.Edge.RuleId is null) continue;
            var rule = state.Rule(step.Edge.RuleId);
            if (!rule.Confirmed) { unknown = true; continue; }
            if (TransportTriggers(rule, items)) knownTargets.Add(state.Node(step.To).Name);
        }
        string text = knownTargets.Count > 0 ? "已知运输后果：入侵 " + string.Join("、", knownTargets.Distinct()) + "。" : "暂无已知运输后果。";
        if (unknown) text += " 路线上仍有未确认规则；已有线索仅能提示不确定性，可在满足物理条件时承担风险，不能据此判断安全。";
        return new(true, 0, text);
    }

    public static string JobName(JobKind kind) => kind switch
    {
        JobKind.Survey => "保守调查", JobKind.Recovery => "标准回收", JobKind.Manual => "手操出勤",
        JobKind.Build => "接管建设", JobKind.Maintenance => "常规维护", JobKind.Repair => "通路抢修", JobKind.Recruit => "扩编",
        JobKind.ContrastSurvey => "对照调查", JobKind.Supply => "隔离工具补充", JobKind.HeadquartersSeal => "总部补封装", JobKind.Research => "施工·研究", _ => "未知事务"
    };

    private Assignment Prepare(Assignment original)
    {
        var result = Clone(original);
        var node = state.Nodes.FirstOrDefault(n => n.Id == result.NodeId);
        if (node is null) return result;
        if (string.IsNullOrEmpty(result.Id))
        {
            int sequence = 1;
            do { result.Id = $"day{state.Day}-{sequence++}"; } while (state.Plan.Any(p => p.Id == result.Id));
        }
        result.Cost = Cost(result.Kind, node);
        if (result.Kind == JobKind.Supply && result.TargetItemId == "") result.TargetItemId = "seal-a";
        if (result.Kind is JobKind.HeadquartersSeal or JobKind.Research && result.TargetItemId == "")
            result.TargetItemId = state.Items.FirstOrDefault(i => i.Kind == "Lamp" && i.Location == ItemLocation.Warehouse)?.Id ?? "";
        if (result.Kind == JobKind.Survey && result.SurveyTemplate == "") result.SurveyTemplate = MirrorRule(node) is null ? "records" : "history";
        if (result.Kind == JobKind.ContrastSurvey && result.SurveyTemplate == "")
            result.SurveyTemplate = node.Rooms.Any(r => r.HasArchiveCabinet && r.ArchiveSearchRemaining > 0) ? "contrast-main" : "contrast-reserve";
        if (result.Route.Count == 0) result.Route = FindRoute(result.NodeId);
        if (result.EmployeeIds.Count == 0)
        {
            var used = state.Plan.SelectMany(p => p.EmployeeIds).Concat(state.Stations.Values).ToHashSet();
            result.EmployeeIds = state.Employees.Where(e => !Recovering(e) && !used.Contains(e.Id)).Take(StaffCost(result.Kind)).Select(e => e.Id).ToList();
        }
        if (result.ToolIds.Count == 0)
        {
            var used = state.Plan.SelectMany(p => p.ToolIds).ToHashSet();
            var kit = state.Items.FirstOrDefault(i => i.Kind == "SealKit" && i.Location == ItemLocation.Warehouse && i.AvailableDay <= state.Day && i.Charges > 0 && i.ReservedBy is null && !used.Contains(i.Id));
            if (result.Kind == JobKind.Supply)
            {
                if (state.Items.Any(i => i.Id == result.TargetItemId)) result.ToolIds.Add(result.TargetItemId);
                var refill = state.Items.FirstOrDefault(i => i.Kind == "SupplyCharge" && i.Location == ItemLocation.Warehouse && i.Charges > 0 && !used.Contains(i.Id));
                if (refill is not null) result.ToolIds.Add(refill.Id);
            }
            else if (result.Kind == JobKind.HeadquartersSeal && kit is not null) result.ToolIds.Add(kit.Id);
            else if (result.Kind is JobKind.Recovery or JobKind.Manual)
            {
                if (state.Scenario == "M2" && node.IsSource)
                {
                    var box = state.Items.FirstOrDefault(i => i.Kind == "SourceCase" && i.ContractNodeId == node.Id && i.Location == ItemLocation.Warehouse &&
                        i.AvailableDay <= state.Day && i.Charges > 0 && i.ReservedBy is null && !used.Contains(i.Id));
                    if (box is not null && node.RuleIds.Select(state.Rule).All(r => r.Confirmed && r.Archived)) result.ToolIds.Add(box.Id);
                }
                else if (kit is not null) result.ToolIds.Add(kit.Id);
            }
            if (result.Kind == JobKind.Manual)
            {
                var returningTarget = Target(node);
                if (returningTarget is { Location: ItemLocation.Warehouse, Sealed: false } && !used.Contains(returningTarget.Id))
                    result.ToolIds.Add(returningTarget.Id);
            }
        }
        return result;
    }

    private ActionResult ValidatePlan(IReadOnlyList<Assignment> tasks)
    {
        if (state.Phase != DayPhase.Preparation) return ActionResult.Fail("当前安排已经锁定。");
        if (tasks.Count > state.TransactionLimit) return ActionResult.Fail($"事务位不足：每日最多 {state.TransactionLimit} 项。");
        if (tasks.Count(t => t.Kind == JobKind.Manual) > 1) return ActionResult.Fail("每日最多 1 项手操出勤。");
        if (tasks.Select(t => t.Id).Distinct().Count() != tasks.Count) return ActionResult.Fail("任务 ID 重复。");
        var usedEmployees = new HashSet<string>();
        var usedTools = new HashSet<string>();
        var usedTargets = new HashSet<string>();
        var usedSupport = new HashSet<string>();
        foreach (var station in state.Stations)
        {
            var node = state.Nodes.FirstOrDefault(n => n.Id == station.Key);
            var employee = state.Employees.FirstOrDefault(e => e.Id == station.Value);
            if (node is null || !node.Built || employee is null || Recovering(employee)) return ActionResult.Fail("驻站岗位或员工已失效。");
            if (!usedEmployees.Add(employee.Id)) return ActionResult.Fail($"员工冲突：{employee.Name} 被多个驻站占用。");
        }
        foreach (var task in tasks)
        {
            if (!Enum.IsDefined(task.Kind)) return ActionResult.Fail("任务种类无效。");
            var node = state.Nodes.FirstOrDefault(n => n.Id == task.NodeId);
            if (node is null) return ActionResult.Fail("任务地点不存在。");
            if (task.Completed || task.Paid) return ActionResult.Fail("不能将已结算或已预付任务重新提交。");
            if (task.EmployeeIds.Count != StaffCost(task.Kind)) return ActionResult.Fail($"人力不足：{JobName(task.Kind)} 需要 {StaffCost(task.Kind)} 名未占用员工。");
            foreach (var id in task.EmployeeIds)
            {
                var employee = state.Employees.FirstOrDefault(e => e.Id == id);
                if (employee is null || Recovering(employee)) return ActionResult.Fail($"员工 {id} 不存在或正在恢复。");
                if (!usedEmployees.Add(id)) return ActionResult.Fail($"员工冲突：{employee.Name} 已被任务或驻站占用。");
            }
            foreach (var id in task.ToolIds)
            {
                var tool = state.Items.FirstOrDefault(i => i.Id == id);
                if (tool is null || tool.Location != ItemLocation.Warehouse || (tool.Charges <= 0 && !(task.Kind == JobKind.Supply && tool.Id == task.TargetItemId)) ||
                    tool.AvailableDay > state.Day || tool.Disposition != "" || tool.ReservedBy is not null)
                    return ActionResult.Fail($"{tool?.Name ?? "所选工具"} 不在可用仓库、已耗尽、尚未生效或已预留处置。");
                if (!usedTools.Add(id)) return ActionResult.Fail($"唯一工具冲突：{tool.Name} 不能同时配发两队。");
                if (tool.Kind == "SourceCase" && (tool.ContractNodeId != node.Id || !node.IsSource || node.RuleIds.Select(state.Rule).Any(r => !r.Confirmed || !r.Archived)))
                    return ActionResult.Fail("源头专用箱只在源头规程归档确认后，配发给对应源头作业，不能移用于其他物品。");
            }
            if (task.TargetItemId != "" && !task.ToolIds.Contains(task.TargetItemId))
            {
                var targetItem = state.Items.FirstOrDefault(i => i.Id == task.TargetItemId);
                if (targetItem is null || targetItem.Location != ItemLocation.Warehouse || targetItem.AvailableDay > state.Day || targetItem.ReservedBy is not null ||
                    targetItem.Disposition != "" || !usedTools.Add(targetItem.Id)) return ActionResult.Fail("处置目标不可用或与其他任务重复占用同一实物。");
            }
            if (task.SupportItemId != "")
            {
                var lamp = state.Items.FirstOrDefault(i => i.Id == task.SupportItemId);
                if (!IsField(task.Kind) || lamp is null || lamp.Kind != "Lamp" || lamp.Location != ItemLocation.Installed ||
                    lamp.SupportUsedDay == state.Day || lamp.ReservedBy is not null || !OperatingNodeIds().Contains(lamp.InstalledNodeId))
                    return ActionResult.Fail("灯具支援需要有效营业点的已安装灯，本日尚未使用。");
                if (!usedSupport.Add(lamp.Id)) return ActionResult.Fail("同一灯具的经营日额度不能分配给两支队伍。");
            }
            if (task.Cost != Cost(task.Kind, node)) return ActionResult.Fail("任务费用已变更，请重新安排。");
            if (task.Kind != JobKind.Repair && (task.Route.Count == 0 || task.Route[0] != state.Nodes.First(n => n.IsHeadquarters).Id || task.Route[^1] != node.Id || !ValidPath(task.Route)))
                return ActionResult.Fail($"{node.Name} 当前没有所选合法通路；不能依赖同日尚未完成的抢修。");
            if (IsField(task.Kind) && !usedTargets.Add(node.Id)) return ActionResult.Fail($"唯一目标冲突：{node.Name} 已有当日作业。");
            if (task.Kind is JobKind.Build or JobKind.Maintenance or JobKind.Repair && !usedTargets.Add($"{task.Kind}:{node.Id}"))
                return ActionResult.Fail("同一工程目标不能重复安排。");
            if (task.Kind is JobKind.Manual && node.Rooms.Count == 0) return ActionResult.Fail("该节点没有手操现场。");
            if (task.Kind == JobKind.Survey && (!node.RuleIds.Any(id => !state.Rule(id).EvidenceFound) ||
                (MirrorRule(node) is not null && task.SurveyTemplate != "history"))) return ActionResult.Fail("此处没有尚未取得的历史或可靠调查记录；已取得历史后可选择对照调查。");
            if (task.Kind == JobKind.ContrastSurvey)
            {
                var mirror = MirrorRule(node);
                if (mirror is null || !mirror.EvidenceFound || !mirror.Archived || mirror.ArchivedContrastObservation || mirror.HasContrastObservation)
                    return ActionResult.Fail("对照调查需要已归档历史且尚无本实例可区分观察；只返回真实记录，不代选答案。");
                if (task.ToolIds.Count != 0) return ActionResult.Fail("对照模板使用公开标准护符防护，不配发或消耗便携隔离工具。");
                if (task.SurveyTemplate == "contrast-main" && !node.Rooms.Any(r => r.HasArchiveCabinet && r.ArchiveSearchRemaining > 0) ||
                    task.SurveyTemplate == "contrast-reserve" && mirror.ContrastSamplesRemaining <= 0 ||
                    task.SurveyTemplate is not ("contrast-main" or "contrast-reserve"))
                    return ActionResult.Fail("所选对照来源已用尽；不能刷新旧柜，只能使用仍有库存的公开登记样本。");
            }
            if (task.Kind == JobKind.Recovery)
            {
                var target = Target(node);
                if (target is null || target.Location != ItemLocation.Room || target.ReservedBy is not null || node.RecoveryPaid) return ActionResult.Fail("唯一合同目标不存在、已回收或已被占用。");
                if (RequiredRules(node, task.Route).Any(r => !r.Confirmed || !r.Archived)) return ActionResult.Fail("必需规程尚未依据归档证据确认；先调查并判断候选。");
                if (state.Scenario == "M2" && node.IsSource)
                {
                    if (!task.ToolIds.Any(id => state.Item(id).Kind == "SourceCase" && state.Item(id).ContractNodeId == node.Id && state.Item(id).Charges >= 1))
                        return ActionResult.Fail("缺少本源头专用箱；不能使用便携隔离工具替代。");
                }
                else if (!task.ToolIds.Any(id => state.Item(id).Kind == "SealKit" && state.Item(id).Charges >= 1)) return ActionResult.Fail("缺少至少 1 次可用隔离工具；必须配发唯一真实实例，不能使用本日才补入的次数。");
                if (target.RequiresLightForSealing && !state.TeamDeck.Contains(CardKind.Light)) return ActionResult.Fail("此回收的公开收容要求照明，当前队伍配置缺少点灯能力。");
            }
            if (task.Kind == JobKind.Build && (node.IsHeadquarters || node.IsSource || node.Built ||
                (!node.PublicCooperation && (!node.RecoveryPaid || node.RuleIds.Select(state.Rule).Any(r => !r.Confirmed || !r.Archived)))))
                return ActionResult.Fail("未满足接管资格：需已交付合同实物且已确认归档规程，或满足公开合作条件；本日未完成成果不能提前使用。");
            if (task.Kind == JobKind.Maintenance && (!node.Built || (!node.Invaded && node.Maintained) || RequiredRules(node, task.Route).Any(r => !r.Confirmed || !r.Archived)))
                return ActionResult.Fail("维护需要已建成、存在待处理入侵或维护缺口，并已掌握所需规程。");
            if (task.Kind == JobKind.Repair && !state.Edges.Any(e => !e.Passable && (e.From == node.Id || e.To == node.Id))) return ActionResult.Fail("该节点没有需要抢修的相邻中断通路。");
            if (task.Kind == JobKind.Recruit && (state.Scenario == "M2" || state.Expanded || tasks.Count(t => t.Kind == JobKind.Recruit) > 1)) return ActionResult.Fail("M2 固定六人不扩编；旧验证版只允许扩编一次。");
            if (task.Kind is JobKind.Supply or JobKind.HeadquartersSeal or JobKind.Research)
            {
                if (!node.IsHeadquarters) return ActionResult.Fail("该项工程只在公司总部受理。");
                var target = state.Items.FirstOrDefault(i => i.Id == task.TargetItemId);
                if (target is null || target.Location != ItemLocation.Warehouse || target.AvailableDay > state.Day || target.Disposition != "")
                    return ActionResult.Fail("请选择已归库、尚未处置且本日可用的真实目标。");
                if (task.Kind == JobKind.Supply && (target.Kind != "SealKit" || target.Charges >= target.MaxCharges || !task.ToolIds.Contains(target.Id) ||
                    !task.ToolIds.Select(state.Item).Any(i => i.Kind == "SupplyCharge" && i.Charges > 0)))
                    return ActionResult.Fail("补充需要未满容量的隔离工具与供应商唯一替换耗材；满额或耗材已用不受理。");
                if (task.Kind == JobKind.HeadquartersSeal && (target.Required || target.Kind is not ("Lamp" or "Anomaly") || target.Sealed ||
                    !task.ToolIds.Select(state.Item).Any(i => i.Kind == "SealKit" && i.Charges > 0)))
                    return ActionResult.Fail("总部补封装需要未封装可选实物和当前可用的隔离次数；不能借同日补入次数。");
                if (task.Kind == JobKind.Research && (target.Required || !target.Sealed || target.Kind is not ("Lamp" or "Anomaly")))
                    return ActionResult.Fail("研究只消耗已封装的可选仓库实物；安装、出售或合同物不能同时研究。");
            }
        }
        if (tasks.Sum(t => t.Cost) > state.Money) return ActionResult.Fail($"预付资金不足：需要 {tasks.Sum(t => t.Cost)}，现有 {state.Money}。");
        return ActionResult.Ok();
    }

    private IEnumerable<RuleState> RequiredRules(CityNode node, IEnumerable<string> route)
        => node.RuleIds.Select(state.Rule)
            .Concat(PathEdges(route.ToList()).Where(s => s.Edge.RuleId is not null).Select(s => state.Rule(s.Edge.RuleId!)))
            .Concat(state.Rules.Where(r => r.Scope == RuleScope.City)).DistinctBy(r => r.Id);

    private void ExecuteDelegated(Assignment task)
    {
        var node = state.Node(task.NodeId);
        switch (task.Kind)
        {
            case JobKind.Survey:
                var clue = node.RuleIds.Select(state.Rule).FirstOrDefault(r => !r.EvidenceFound);
                if (clue is not null)
                {
                    clue.EvidenceFound = true;
                    if (!clue.EvidenceRecords.Contains(clue.Clue)) clue.EvidenceRecords.Add(clue.Clue);
                    state.Record($"{node.Name} 保守调查取得记录：{clue.Clue}");
                    state.LastDaySummary.Add($"{node.Name}：历史或可靠记录已带回，日终归档后次日可用于判断。");
                }
                break;
            case JobKind.ContrastSurvey:
                var mirror = MirrorRule(node)!;
                if (task.SurveyTemplate == "contrast-main")
                {
                    var cabinet = node.Rooms.First(r => r.HasArchiveCabinet && r.ArchiveSearchRemaining > 0);
                    cabinet.ArchiveSearchRemaining--;
                    cabinet.ContainerLocated = true;
                    cabinet.SearchUses++;
                }
                else mirror.ContrastSamplesRemaining--;
                mirror.HasContrastObservation = true;
                mirror.Observed = true;
                mirror.ContrastTriggered = !mirror.MirrorVariantB;
                string sample = task.SurveyTemplate == "contrast-main" ? "镜面房原档案柜" : "事先登记的备用对照柜";
                string observation = $"{sample}：未照明，独立出勤本房正常搜查 0→1；" +
                    (mirror.ContrastTriggered == true ? "观察到反噬，护符 1→0，心志 12→12。" : "未观察到反噬，护符 1→1，心志 12→12。") +
                    "仅带回寻物记录，合同物仍未发现、未领取；隔离工具次数不变。";
                mirror.EvidenceRecords.Add(observation);
                state.Record(observation);
                state.LastDaySummary.Add("对照观察已带回；候选由玩家根据事实判断，归档后次日可用。");
                break;
            case JobKind.Recovery:
                var item = state.Item(node.TargetItemId);
                var tool = task.ToolIds.Select(state.Item).First(i => state.Scenario == "M2" && node.IsSource ? i.Kind == "SourceCase" : i.Kind == "SealKit");
                tool.Charges--;
                item.Discovered = true;
                item.Sealed = true;
                foreach (var cabinet in node.Rooms.Where(r => r.HasArchiveCabinet && r.ArchiveSearchRemaining > 0))
                { cabinet.ArchiveSearchRemaining = 0; cabinet.ContainerLocated = true; }
                ApplyTransport(new() { item }, task.Route.AsEnumerable().Reverse().ToList());
                item.Location = ItemLocation.Warehouse;
                item.OwnerId = "";
                state.LastDaySummary.Add($"{node.Name} 回收归队：{item.Name} 已封装；{tool.Name} 剩余 {tool.Charges} 次。");
                break;
            case JobKind.Build:
                node.Built = true;
                node.BuiltDay = state.Day;
                node.Maintained = true;
                state.Record($"{node.Name} 建设完成；次日起可营业、封控与交接。");
                state.LastDaySummary.Add($"建成 {node.Name}：本日不计营业、封控或交接，次日起安排驻站生效。");
                break;
            case JobKind.Maintenance:
                node.Invaded = false;
                node.InvasionSources.Clear();
                node.Maintained = true;
                node.MaintenanceCount++;
                state.Record($"{node.Name} 完成规程维护；没有重新支付基础建设费。");
                state.LastDaySummary.Add($"维护恢复 {node.Name}；旧点晨间留人可参加今夜封控与交接，无补发收入。");
                break;
            case JobKind.Repair:
                foreach (var edge in state.Edges.Where(e => !e.Passable && (e.From == node.Id || e.To == node.Id))) edge.Passable = true;
                state.Record($"{node.Name} 的相邻中断通路已恢复；线路规则仍保留。");
                state.LastDaySummary.Add($"抢修完成：{node.Name} 相邻通路恢复；不消除已知或未知线路守则。");
                break;
            case JobKind.Supply:
                var kit = state.Item(task.TargetItemId);
                var supply = task.ToolIds.Select(state.Item).First(i => i.Kind == "SupplyCharge");
                supply.Charges--;
                supply.Location = ItemLocation.Consumed;
                kit.Charges = Math.Min(kit.MaxCharges, kit.Charges + 1);
                kit.AvailableDay = state.Day + 1;
                state.Record($"{kit.Name} 完成有限补充，余量 {kit.Charges}/{kit.MaxCharges}；替换耗材已消耗，次日才可派发。");
                state.LastDaySummary.Add($"补充完成：{kit.Name} +1 次，次日可用；唯一供应已耗尽。");
                break;
            case JobKind.HeadquartersSeal:
                var optional = state.Item(task.TargetItemId);
                var sealingKit = task.ToolIds.Select(state.Item).First(i => i.Kind == "SealKit");
                sealingKit.Charges--;
                optional.Sealed = true;
                optional.AvailableDay = state.Day + 1;
                state.Record($"总部完成 {optional.Name} 补封装，隔离工具剩 {sealingKit.Charges} 次；次日才可出售或安装。");
                state.LastDaySummary.Add($"总部补封装：{optional.Name}，消耗隔离工具 1 次；无额外现金费，次日处置。");
                break;
            case JobKind.Research:
                var research = state.Item(task.TargetItemId);
                research.Location = ItemLocation.Consumed;
                state.Record($"施工·研究完成：消耗唯一 {research.Name}，登记研究档案；未附加未声明收入或能力。");
                state.LastDaySummary.Add($"施工·研究：{research.Name} 已消耗，不能再次出售、安装或研究。");
                break;
            case JobKind.Recruit:
                state.Expanded = true;
                state.DailyExpense++;
                state.Employees.AddRange(new[] { new Employee { Id = "e7", Name = "周遥" }, new Employee { Id = "e8", Name = "方白" } });
                break;
        }
        task.Completed = true;
        task.Successful = true;
        ReleaseReservations(task);
    }

    private void SettleSurvey(CityNode node)
    {
        if (!node.RuleIds.Select(state.Rule).Any(r => r.EvidenceFound)) return;
        if (RewardOnce($"survey:{node.Id}", node.SurveyReward, "调查合同", node.Name)) node.SurveyPaid = true;
    }

    private void Deliver(ItemState item)
    {
        var node = state.Nodes.FirstOrDefault(n => n.Id == item.ContractNodeId && n.TargetItemId == item.Id);
        if (node is null || node.RecoveryPaid) return;
        ItemState? sourceCase = null;
        if (state.Scenario == "M2" && node.IsSource)
        {
            sourceCase = state.Items.FirstOrDefault(i => i.Kind == "SourceCase" && i.ContractNodeId == node.Id && i.Location == ItemLocation.Warehouse && i.Charges == 0);
            if (sourceCase is null) { state.Record("源头交付尚缺同次收容的专用箱实物，请先取回归库；不会复制或免费补发。"); return; }
        }
        item.Location = ItemLocation.Delivered;
        item.OwnerId = "";
        if (sourceCase is not null) { sourceCase.Location = ItemLocation.Delivered; sourceCase.OwnerId = ""; }
        if (RewardOnce($"recovery:{node.Id}", node.RecoveryReward, "实物交付合同", node.Name)) node.RecoveryPaid = true;
        state.LastDaySummary.Add($"实际交付 {item.Name}：{node.Name} 合同到账 {node.RecoveryReward}。");
        if (node.IsSource) { state.SourceRecovered = true; state.Record("源头实物已交付，尚未执行的该源头箭头取消；已经发生的运输入侵保留。"); }
    }

    private void ApplyTransport(List<ItemState> items, List<string> route)
    {
        foreach (var step in PathEdges(route))
        {
            if (step.Edge.RuleId is null) continue;
            var rule = state.Rule(step.Edge.RuleId);
            if (!TransportTriggers(rule, items)) continue;
            var target = state.Node(step.To);
            target.Invaded = true;
            if (!target.InvasionSources.Contains($"transport:{rule.Id}")) target.InvasionSources.Add($"transport:{rule.Id}");
            rule.Observed = true;
            state.Record($"实际运输后果：{target.Name} 出现入侵（运输守则事件）；交付源头不会撤销该事件。");
        }
    }

    private static bool TransportTriggers(RuleState rule, IEnumerable<ItemState> items)
        => rule.Trigger == RuleTrigger.TransportUnsealed && items.Any(i => (i.Kind is "Anomaly" or "Lamp" || i.RequiresSealing) && !i.Sealed);

    private void ApplySpread()
    {
        // Eligibility and all blocked arrows are decided before ANY enemy consequence is applied.
        var operational = OperatingNodeIds().ToHashSet();
        var blockades = state.Nodes.Where(n => operational.Contains(n.Id) && n.BlockadeEdgeId is not null &&
            (n.PublicCooperation || RequiredRules(n, FindRoute(n.Id)).All(r => r.Confirmed && r.Archived))).Select(n => n.BlockadeEdgeId!).ToHashSet();
        var resolutions = state.Forecast.Select(intent => new
        {
            Intent = intent,
            Cancelled = state.Node(intent.SourceId).IsSource && state.SourceRecovered,
            Blocked = blockades.Contains(intent.EdgeId)
        }).ToList();
        foreach (var outcome in resolutions)
        {
            if (outcome.Cancelled) { state.Record($"已回收来源，取消其预告箭头：{state.Node(outcome.Intent.TargetId).Name}。"); state.LastDaySummary.Add($"来源已回收，取消对 {state.Node(outcome.Intent.TargetId).Name} 的预告扩张。"); continue; }
            if (outcome.Blocked) { state.Record($"共同快照封控成功：{state.Node(outcome.Intent.TargetId).Name}；不改投目标。"); state.LastDaySummary.Add($"封控成功：阻止对 {state.Node(outcome.Intent.TargetId).Name} 的扩张。"); continue; }
            var target = state.Node(outcome.Intent.TargetId);
            target.Invaded = true;
            if (!target.InvasionSources.Contains(outcome.Intent.SourceId)) target.InvasionSources.Add(outcome.Intent.SourceId);
            if (target.Kind == "Hub")
            {
                foreach (var edge in state.Edges.Where(e => e.From == target.Id || e.To == target.Id)) edge.Passable = false;
            }
            state.Record($"预告扩张落实：{state.Node(outcome.Intent.SourceId).Name} → {target.Name}；本晚不继续连锁。");
            state.LastDaySummary.Add($"预告扩张落实：{target.Name} 入侵；当晚不继续连锁。");
        }
    }

    private void GenerateForecast()
    {
        state.Forecast.Clear();
        if (!state.PressureEnabled || state.SourceRecovered || state.Day % 2 != 0) return;
        var source = state.Nodes.First(n => n.IsSource);
        var edge = state.Edges.First(e => e.From == source.Id || e.To == source.Id);
        string target = edge.From == source.Id ? edge.To : edge.From;
        state.Forecast.Add(new SpreadIntent { SourceId = source.Id, EdgeId = edge.Id, TargetId = target,
            Description = $"当晚 {source.Name} → {state.Node(target).Name}：入侵并切断该相邻通路；回收来源可取消，相邻有效服务点封控可阻止。" });
    }

    private bool RewardOnce(string key, int amount, string category, string description)
    {
        if (state.PaidRewards.Contains(key)) return false;
        state.PaidRewards.Add(key);
        if (amount != 0) Pay(amount, category, description);
        return true;
    }

    private void Pay(int amount, string category, string description)
    {
        state.Money += amount;
        state.Ledger.Add(new LedgerEntry { Day = state.Day, Category = category, Description = description, Amount = amount, Balance = state.Money });
    }

    private void ReleaseReservations(Assignment task)
    {
        foreach (var item in state.Items.Where(i => i.ReservedBy == task.Id)) item.ReservedBy = null;
    }

    private IEnumerable<(CityEdge Edge, string To)> PathEdges(IReadOnlyList<string> route)
    {
        for (int i = 1; i < route.Count; i++)
        {
            var edge = state.Edges.FirstOrDefault(e => e.From == route[i - 1] && e.To == route[i] || e.To == route[i - 1] && e.From == route[i]);
            if (edge is not null) yield return (edge, route[i]);
        }
    }

    private bool ValidPath(IReadOnlyList<string> path) => path.Count > 0 && path.All(id => state.Nodes.Any(n => n.Id == id)) &&
        PathEdges(path).Count() == path.Count - 1 && PathEdges(path).All(s => s.Edge.Passable);
    private bool Recovering(Employee employee) => employee.RecoverUntilDay > state.Day;
    private ItemState? Target(CityNode node) => state.Items.FirstOrDefault(i => i.Id == node.TargetItemId);
    private RuleState? MirrorRule(CityNode node) => node.RuleIds.Select(state.Rule).FirstOrDefault(r => r.Trigger == RuleTrigger.MirrorSearch);
    private string HeadquartersId() => state.Nodes.First(n => n.IsHeadquarters).Id;
    private bool PlannedItem(string itemId) => (state.Phase == DayPhase.Preparation ? state.Plan : state.LockedPlan.Where(t => !t.Completed))
        .Any(t => t.ToolIds.Contains(itemId) || t.TargetItemId == itemId || t.SupportItemId == itemId);
    private static bool IsField(JobKind kind) => kind is JobKind.Survey or JobKind.Recovery or JobKind.Manual or JobKind.ContrastSurvey;
    private static int StaffCost(JobKind kind) => IsField(kind) ? 2 : 1;
    private static int Cost(JobKind kind, CityNode node) => kind switch { JobKind.Build => 4, JobKind.Recruit => 8, JobKind.Repair => 2,
        JobKind.Supply => 3, JobKind.HeadquartersSeal or JobKind.Research => 0, _ => 1 + (IsField(kind) ? node.TravelCost : 0) };
    private static Assignment Clone(Assignment a) => new()
    {
        Id = a.Id, Kind = a.Kind, NodeId = a.NodeId, EmployeeIds = a.EmployeeIds.ToList(), ToolIds = a.ToolIds.ToList(),
        Route = a.Route.ToList(), KnownRuleIds = a.KnownRuleIds.ToList(), Cost = a.Cost, Completed = a.Completed,
        Successful = a.Successful, Paid = a.Paid, TargetItemId = a.TargetItemId, SupportItemId = a.SupportItemId, SurveyTemplate = a.SurveyTemplate
    };
}
