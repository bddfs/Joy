using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Joy.Core;

namespace Joy.Tests;

public static class CityTests
{
    public static void Run(Action<bool, string> check)
    {
        CheckPlanning(check);
        CheckKnowledgeAndRewards(check);
        CheckBuildRepairAndMaintenance(check);
        CheckSnapshotSpread(check);
        CheckTransportSecrecy(check);
        CheckEconomyAndRecovery(check);
        foreach (int seed in new[] { 1, 413, 495660 })
        {
            ProveRoute(seed, false, check);
            ProveRoute(seed, true, check);
        }
    }

    private static void CheckPlanning(Action<bool, string> check)
    {
        var s = WorldFactory.CreateLegacy();
        var engine = new CityEngine(s);
        check(s.Nodes.Count == 7 && s.Employees.Count == 6 && s.TransactionLimit == 2 && s.Edges.Count >= s.Nodes.Count &&
              s.Nodes.All(n => engine.FindRoute(n.Id).Count > 0), "AC-08：七节点、六员工、二事务及连通环路测试骨架");
        int money = s.Money;
        check(engine.AddTask(JobKind.Survey, "hotel", new[] { "e1", "e2" }).Success, "AC-01：首队合法安排");
        check(!engine.AddTask(JobKind.Survey, "source", new[] { "e2", "e3" }).Success && s.Plan.Count == 1 && s.Money == money,
            "AC-01：重复员工拒绝且不改变资金或安排");
        check(engine.AddTask(JobKind.Survey, "source", new[] { "e3", "e4" }).Success, "AC-01：第二队使用独立员工");
        check(!engine.AddTask(JobKind.Survey, "hospital").Success && s.Plan.Count == 2, "AC-01：第三项超出事务位被拒绝");
        s.Node("depot").Built = true;
        check(engine.AssignStation("depot", "e5").Success && !engine.AssignStation("depot", "e1").Success,
            "AC-01：驻站占同一人力池但不占事务位");
        check(engine.LockDay().Success && s.Money == money - 2 && s.LockedPlan.All(p => p.Paid), "AC-03：锁定时完整预付一次");
        check(!engine.LockDay().Success && !engine.RemoveTask(s.Plan[0].Id).Success && s.Money == money - 2, "AC-02/03：锁定后不能重排或重复扣费");
        check(engine.SettleDay().Success && s.Day == 2, "AC-03：多队仅推进一次经营日");
        money = s.Money;
        check(!engine.SettleDay().Success && s.Day == 2 && s.Money == money, "AC-03：日结不能重放");

        s = WorldFactory.CreateLegacy(); engine = new CityEngine(s);
        check(engine.AddTask(JobKind.Manual, "hotel").Success && !engine.AddTask(JobKind.Manual, "source").Success,
            "AC-01：一天最多一项手操");
        s = WorldFactory.CreateLegacy(); engine = new CityEngine(s); Know(s, "hotel-dark", "hotel-knock", "source-carry");
        check(engine.AddTask(JobKind.Recovery, "hotel", tools: new[] { "seal-a" }).Success &&
              !engine.AddTask(JobKind.Recovery, "source", tools: new[] { "seal-a" }).Success, "AC-01/09：唯一工具不能配发两队");
        check(!engine.AddTask(JobKind.Recovery, "hotel", tools: new[] { "seal-b" }).Success, "AC-01：同一合同目标不能被重复安排");
        check(engine.AddTask(JobKind.Recovery, "source", tools: new[] { "seal-b" }).Success && engine.LockDay().Success &&
              s.Item("seal-a").ReservedBy != s.Item("seal-b").ReservedBy && s.Item("hotel-clock").ReservedBy is not null,
            "AC-01：工具与唯一目标在锁定时明确预留");
        check(engine.SettleDay().Success && s.Item("hotel-clock").Location == ItemLocation.Delivered &&
              s.Item("source-box").Location == ItemLocation.Delivered && s.Item("seal-a").Charges == 1 && s.Item("seal-b").Charges == 1 && s.Money == 25,
            "AC-09/16：双回收使用真实次数并唯一交付，账目为 16−2+5+8−2=25");
        check(!engine.AddTask(JobKind.Recovery, "hotel").Success && s.Items.Count(i => i.Id == "hotel-clock") == 1,
            "AC-16：已回收实物不能切换模式再次获得");
        s = WorldFactory.CreateLegacy(); engine = new CityEngine(s); Know(s, "hotel-dark", "hotel-knock"); s.Item("seal-a").Charges = 0;
        check(!engine.AddTask(JobKind.Recovery, "hotel", tools: new[] { "seal-a" }).Success, "AC-09：耗尽工具在扣费前被拒绝");
    }

    private static void CheckKnowledgeAndRewards(Action<bool, string> check)
    {
        var s = WorldFactory.CreateLegacy(); var engine = new CityEngine(s);
        check(!engine.ConfirmRule("hotel-dark", 0).Success, "AC-10：证据不足不能城市确认");
        Need(engine.AddTask(JobKind.Survey, "hotel")); Need(engine.LockDay()); Need(engine.SettleDay());
        check(s.Rule("hotel-dark").EvidenceFound && s.Rule("hotel-dark").Archived, "AC-02：委派可靠线索于日终归档");
        int money = s.Money;
        Need(engine.ConfirmRule("hotel-dark", 1));
        check(!engine.ConfirmRule("hotel-dark", 0).Success && !s.Rule("hotel-dark").Confirmed && s.Rule("hotel-dark").WrongCandidates.SequenceEqual(new[] { 1 }),
            "AC-10：错误候选保留，每条规则每天一次城市判断");
        Need(engine.LockDay()); Need(engine.SettleDay());
        Need(engine.ConfirmRule("hotel-dark", 0));
        check(s.Rule("hotel-dark").Confirmed && s.Rule("hotel-dark").ConfirmationRewardClaimed && s.Money == money - 2,
            "AC-10：次日可确认，城市确认不发现场零钱且共享领奖标记");
        Need(engine.AddTask(JobKind.Survey, "hotel")); Need(engine.LockDay()); Need(engine.SettleDay());
        check(s.PaidRewards.Count(k => k == "survey:hotel") == 1 && s.Rule("hotel-knock").Archived,
            "AC-03/10：同地后续调查返回新线索且不重复首次里程碑奖励");

        s = WorldFactory.CreateLegacy(); engine = new CityEngine(s);
        s.Rule("hotel-dark").Observed = true;
        Need(engine.LockDay()); Need(engine.SettleDay());
        check(s.Rule("hotel-dark").Archived && engine.ConfirmRule("hotel-dark", 0).Success,
            "AC-10：实际观察记录在日终归档后也能用于城市推理，未强制只能调查卡获证据");
        Need(engine.AddTask(JobKind.Survey, "hotel")); Need(engine.LockDay()); Need(engine.SettleDay());
        check(s.Rule("hotel-dark").EvidenceFound && !s.Rule("hotel-knock").EvidenceFound,
            "AC-02：仅观察不消耗保守调查的可靠线索库存");
    }

    private static void CheckBuildRepairAndMaintenance(Action<bool, string> check)
    {
        var s = WorldFactory.CreateLegacy(); var engine = new CityEngine(s);
        check(!engine.AddTask(JobKind.Build, "hotel").Success, "AC-25：到达地点不能直接接管");
        s.Node("hotel").RecoveryPaid = true;
        check(!engine.AddTask(JobKind.Build, "hotel").Success, "AC-25：已交付实物但缺少确认规程，仍不可接管");
        Know(s, "hotel-dark", "hotel-knock");
        Need(engine.AddTask(JobKind.Build, "hotel")); Need(engine.AddTask(JobKind.Build, "depot"));
        Need(engine.LockDay()); Need(engine.SettleDay());
        check(s.Node("hotel").Built && s.Node("hotel").BuiltDay == 1 && !s.Ledger.Any(l => l.Day == 1 && l.Category == "营业") && s.Money == 6,
            "AC-04/25：新建当日不营业、不追领收入，支付两次建设费");
        Need(engine.AssignStation("hotel", "e1")); Need(engine.AssignStation("depot", "e2"));
        Need(engine.LockDay()); Need(engine.SettleDay());
        check(s.Ledger.Count(l => l.Day == 2 && l.Category == "营业") == 2 && s.Money == 6, "AC-04：次日起驻站后两点营业并抵消基本支出");

        s = WorldFactory.CreateLegacy(); engine = new CityEngine(s); Know(s, "hotel-dark", "hotel-knock");
        var hotel = s.Node("hotel"); hotel.Built = true; hotel.BuiltDay = 0; hotel.Invaded = true;
        Need(engine.AssignStation("hotel", "e1")); Need(engine.SetBlockade("hotel", "hotel-junction"));
        s.Forecast.Add(new SpreadIntent { SourceId = "source", EdgeId = "hotel-junction", TargetId = "hotel" });
        Need(engine.AddTask(JobKind.Maintenance, "hotel")); Need(engine.LockDay()); Need(engine.SettleDay());
        check(!hotel.Invaded && hotel.Built && hotel.MaintenanceCount == 1 && !s.Ledger.Any(l => l.Category == "营业") && s.Money == 13,
            "AC-04/07：已锁定驻站旧点维护后可封控，无补发收入或重复建设费");
        check(!engine.AddTask(JobKind.Maintenance, "hotel").Success, "AC-07：已完成维护目标不能反复执行");

        s = WorldFactory.CreateLegacy(); engine = new CityEngine(s); Know(s, "hotel-dark", "hotel-knock");
        hotel = s.Node("hotel"); hotel.Built = true; hotel.BuiltDay = 0;
        foreach (var edge in s.Edges.Where(e => e.From == "hotel" || e.To == "hotel")) edge.Passable = false;
        Need(engine.AssignStation("hotel", "e1"));
        check(engine.FindRoute("hotel").Count == 0 && !engine.AddTask(JobKind.Survey, "hotel").Success,
            "AC-02/07：断联不能借用同日未完成的抢修通路");
        Need(engine.AddTask(JobKind.Repair, "hotel")); Need(engine.LockDay()); Need(engine.SettleDay());
        check(engine.FindRoute("hotel").Count > 0 && hotel.Built && !s.Ledger.Any(l => l.Category == "营业") && s.Rule("line-seal").Trigger == RuleTrigger.TransportUnsealed,
            "AC-07：抢修恢复交通，设施和线路守则保留且不补发旧点收入");
    }

    private static void CheckSnapshotSpread(Action<bool, string> check)
    {
        GameState Fixture(bool reverse)
        {
            var s = WorldFactory.CreateLegacy();
            s.Nodes = new()
            {
                new CityNode { Id = "hq", IsHeadquarters = true }, new CityNode { Id = "hub", Kind = "Hub" },
                new CityNode { Id = "station", Built = true, BuiltDay = 0, PublicCooperation = true, BlockadeEdgeId = "source-station" },
                new CityNode { Id = "source", IsSource = true }
            };
            s.Edges = new()
            {
                new CityEdge { Id = "hq-hub", From = "hq", To = "hub" }, new CityEdge { Id = "hub-station", From = "hub", To = "station" },
                new CityEdge { Id = "source-hub", From = "source", To = "hub" }, new CityEdge { Id = "source-station", From = "source", To = "station" }
            };
            s.Stations["station"] = "e1";
            s.Forecast = new()
            {
                new SpreadIntent { SourceId = "source", EdgeId = "source-hub", TargetId = "hub" },
                new SpreadIntent { SourceId = "source", EdgeId = "source-station", TargetId = "station" }
            };
            if (reverse) s.Forecast.Reverse();
            return s;
        }
        var a = Fixture(false); var b = Fixture(true);
        var ca = new CityEngine(a); var cb = new CityEngine(b);
        Need(ca.LockDay()); Need(cb.LockDay()); Need(ca.SettleDay()); Need(cb.SettleDay());
        check(a.Node("hub").Invaded && !a.Node("station").Invaded && ca.FindRoute("station").Count == 0,
            "AC-05：先算共同封控快照，再同时落实枢纽断联与其他箭头");
        check(a.Nodes.Select(n => n.Invaded).SequenceEqual(b.Nodes.Select(n => n.Invaded)) &&
              a.Edges.Select(e => e.Passable).SequenceEqual(b.Edges.Select(e => e.Passable)) && !a.Node("hq").Invaded,
            "AC-05：交换箭头顺序不改变结果，新侵入节点当晚不二次传播");
    }

    private static void CheckTransportSecrecy(Action<bool, string> check)
    {
        var a = WorldFactory.CreateLegacy(); var b = Clone(a);
        b.Rule("line-seal").Trigger = RuleTrigger.Knock;
        var path = new[] { "source", "relay", "hospital", "hq" };
        var ca = new CityEngine(a); var cb = new CityEngine(b);
        var pa = ca.PreviewTransport(new[] { "source-box" }, path); var pb = cb.PreviewTransport(new[] { "source-box" }, path);
        check(pa == pb && pa.Allowed && !pa.Text.Contains("白榆医院"), "AC-26：未知运输规则改变秘密条件时，按钮、文本、精确节点保持一致");
        a.Rule("line-seal").Confirmed = true;
        check(ca.PreviewTransport(new[] { "source-box" }, path).Text.Contains("白榆医院"), "AC-06/26：确认后才预览已知侵入节点");
        a.Item("source-box").Sealed = true;
        check(!ca.PreviewTransport(new[] { "source-box" }, path).Text.Contains("白榆医院"), "AC-06：封装能消除对应已知运输后果");
        Need(ca.AddTask(new Assignment { Kind = JobKind.Manual, NodeId = "source", Route = path.Reverse().ToList() })); Need(ca.LockDay());
        var manual = a.LockedPlan.Single();
        a.Item("source-box").Location = ItemLocation.Backpack; a.Item("source-box").OwnerId = manual.Id;
        a.Item("hospital-vial").Location = ItemLocation.Backpack; a.Item("hospital-vial").OwnerId = manual.Id;
        a.Field = new FieldState { AssignmentId = manual.Id, NodeId = "source", RoomId = "source-entry", Finished = true, Escaped = true };
        a.Forecast.Add(new SpreadIntent { SourceId = "source", EdgeId = "junction-source", TargetId = "junction" });
        Need(ca.CompleteManual()); Need(ca.SettleDay());
        check(a.SourceRecovered && a.Node("hospital").Invaded && !a.Node("junction").Invaded,
            "AC-06：归队运输先登记，源头交付取消其箭头但不撤销既有运输入侵");
    }

    private static void CheckEconomyAndRecovery(Action<bool, string> check)
    {
        var s = WorldFactory.CreateLegacy(); var engine = new CityEngine(s); s.Money = 2;
        Need(engine.LockDay()); Need(engine.SettleDay());
        check(s.Money == 0 && s.Phase == DayPhase.Preparation, "D-03：恰好付清必要支出，零余额仍可继续");
        Need(engine.LockDay()); Need(engine.SettleDay());
        check(s.Phase == DayPhase.Failed && s.Money == 0, "AC-24：必要经营支出不足进入明确失败且不制造负债");
        s = WorldFactory.CreateLegacy(); engine = new CityEngine(s); s.SourceRecovered = true; s.Money = 0;
        foreach (string id in new[] { "hotel", "depot" }) { s.Node(id).Built = true; s.Node(id).BuiltDay = 0; }
        Need(engine.AssignStation("hotel", "e1")); Need(engine.AssignStation("depot", "e2")); s.DailyExpense = 3;
        Need(engine.LockDay()); Need(engine.SettleDay());
        check(s.Phase == DayPhase.Failed && s.Money == 2, "D-03：同时满足交接但无力支付必要支出时，失败优先");
        s = WorldFactory.CreateLegacy(); engine = new CityEngine(s);
        Need(engine.AddTask(JobKind.Manual, "hotel")); Need(engine.LockDay());
        var task = s.LockedPlan.Single();
        s.Field = new FieldState { AssignmentId = task.Id, NodeId = "hotel", RoomId = "hotel-entry", Finished = true, Escaped = false };
        Need(engine.CompleteManual()); Need(engine.SettleDay());
        check(s.Employees.Count(e => e.Status == EmployeeStatus.Recovery) == 2 && !engine.AddTask(JobKind.Survey, "hotel", task.EmployeeIds).Success,
            "AC-24：失败两名员工下一日无法执行任务");
        Need(engine.LockDay()); Need(engine.SettleDay());
        check(task.EmployeeIds.All(id => s.Employees.First(e => e.Id == id).Status == EmployeeStatus.Standby), "AC-24：完整恢复一经营日后回待命");
        int cash = s.Money; Need(engine.EmergencySupport());
        check(!engine.EmergencySupport().Success && s.Money == cash + 6, "AC-24：紧急支援只可领取一次");
    }

    private static void ProveRoute(int seed, bool mixed, Action<bool, string> check)
    {
        var s = WorldFactory.CreateLegacy(seed); var engine = new CityEngine(s);
        Need(engine.AddTask(mixed ? JobKind.Manual : JobKind.Survey, "hotel"));
        Need(engine.AddTask(JobKind.Survey, "source")); Need(engine.LockDay());
        if (mixed)
        {
            var task = s.LockedPlan.First(p => p.Kind == JobKind.Manual);
            var field = new FieldEngine(s); Need(field.Start(task));
            while (!s.Field!.Hand.Contains(CardKind.Investigate)) Need(field.EndRound());
            Need(field.Execute(new FieldAction { Kind = FieldActionKind.PlayCard, Card = CardKind.Investigate }));
            check(s.Rule("hotel-dark").EvidenceFound && !s.Rule("hotel-dark").Archived &&
                  s.LockedPlan.All(p => !p.KnownRuleIds.Contains("hotel-dark")), $"AC-02：种子 {seed} 手操新证据不会改写锁定任务的已知快照");
            Need(field.Withdraw()); Need(engine.CompleteManual());
            int cash = s.Money; Need(engine.CompleteManual());
            check(s.Money == cash, "AC-18：重复结转手操不重复领取现金");
        }
        Need(engine.SettleDay());
        Need(engine.ConfirmRule("hotel-dark", 0)); Need(engine.ConfirmRule("source-carry", 2));
        Need(engine.AddTask(JobKind.Survey, "hotel")); Need(engine.AddTask(JobKind.Recovery, "source", tools: new[] { "seal-b" }));
        Need(engine.LockDay()); Need(engine.SettleDay());
        Need(engine.ConfirmRule("hotel-knock", 1));
        Need(engine.AddTask(JobKind.Recovery, "hotel", tools: new[] { "seal-a" })); Need(engine.AddTask(JobKind.Build, "depot"));
        Need(engine.LockDay()); Need(engine.SettleDay());
        Need(engine.AddTask(JobKind.Build, "hotel")); Need(engine.LockDay()); Need(engine.SettleDay());
        check(s.Day == 5 && s.Phase == DayPhase.Preparation && s.Node("hotel").BuiltDay == 4 && s.SourceRecovered,
            $"AC-08：种子 {seed} {(mixed ? "混合" : "管理")}路线按真实证据/预付/工具/建设时点推进到第 5 日");
        Need(engine.AssignStation("hotel", "e1")); Need(engine.AssignStation("depot", "e2"));
        Need(engine.LockDay()); Need(engine.SettleDay());
        check(s.Phase == DayPhase.Victory && s.Day == 5 && s.Money == 12 &&
              s.Item("seal-a").Charges == 1 && s.Item("seal-b").Charges == 1 && s.Employees.Count == 6,
            $"AC-08（单城范围）：种子 {seed} {(mixed ? "混合手操" : "纯管理")}真实合法路线 5 日完成，余额 12，封装工具各余 1 次；不代表 AC-33 三城完成");
        int finalMoney = s.Money;
        check(!engine.LockDay().Success && !engine.SettleDay().Success && s.Money == finalMoney,
            "AC-19：完成交接后不能无限等待并领取营业收入");
    }

    private static void Know(GameState s, params string[] ids)
    {
        foreach (string id in ids) { var rule = s.Rule(id); rule.EvidenceFound = true; rule.Archived = true; rule.Confirmed = true; }
    }
    private static void Need(ActionResult result)
    {
        if (!result.Success) throw new InvalidOperationException("城市测试前置操作失败：" + result.Message);
    }
    private static GameState Clone(GameState s) => JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(s))!;
}
