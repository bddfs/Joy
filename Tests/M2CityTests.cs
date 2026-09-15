using System;
using System.Linq;
using System.Text.Json;
using Joy.Core;

namespace Joy.Tests;

/// <summary>M2-specific checks; legacy regression witnesses remain explicitly separate.</summary>
public static class M2CityTests
{
    public static void Run(Action<bool, string> check)
    {
        TopologyAndEvidence(check);
        ToolsSupplyAndStorage(check);
        ManualLoadoutChanges(check);
        LampsAndResearch(check);
        BudgetAndFailure(check);
        foreach (bool variant in new[] { false, true }) ManagementWitness(variant, check);
    }

    private static void TopologyAndEvidence(Action<bool, string> check)
    {
        var s = WorldFactory.Create(mirrorVariantB: false); var city = new CityEngine(s);
        check(s.Scenario == "M2" && s.Nodes.Count == 7 && s.Edges.Count == 7 && s.Employees.Count == 6 &&
              s.Items.Count(i => i.Kind == "SealKit") == 1 && s.Item("seal-a").Charges == 2 && s.Item("seal-a").MaxCharges == 2,
            "M2/AC-36：默认工厂是七节点、六人、唯一初始两次隔离工具");
        string[] route = { "hq", "hotel", "junction", "hospital", "source" };
        check(city.FindRoute("source").SequenceEqual(route) && s.Edges.All(e => !e.Name.Contains("hq") && !e.Name.Contains("hotel")) &&
              !s.PressureEnabled && s.Forecast.Count == 0, "M2：地图采用需求北南环路、中文边名，固定账例关闭传播压力");
        var b = WorldFactory.Create(mirrorVariantB: true);
        check(s.Rule("hotel-mirror").Clue == b.Rule("hotel-mirror").Clue && s.Rule("hotel-mirror").Notice == b.Rule("hotel-mirror").Notice &&
              s.Rule("hotel-mirror").CorrectCandidate != b.Rule("hotel-mirror").CorrectCandidate,
            "AC-34：A/B初始可靠录像一致，真值在实例生成时固定");
        Need(city.AddTask(JobKind.Survey, "hotel")); Day(city);
        check(s.Rule("hotel-mirror").Archived && !s.Rule("hotel-mirror").HasContrastObservation && s.Money == 13,
            "AC-34：历史调查归档但不自动区分A/B，旅馆不发调查收入");
        // Compare the same permitted template before submission on another world with identical public facts.
        var other = Clone(s); other.Rule("hotel-mirror").MirrorVariantB = true; other.Rule("hotel-mirror").CorrectCandidate = 1;
        var previewA = city.PreviewTask(JobKind.ContrastSurvey, "hotel");
        check(previewA.Allowed && previewA == new CityEngine(other).PreviewTask(JobKind.ContrastSurvey, "hotel"),
            "AC-26/34：对照委派提交前不按未知真值改变预览、费用或可用性");
        Need(city.AddTask(JobKind.ContrastSurvey, "hotel"));
        Need(city.LockDay());
        check(!s.Rule("hotel-mirror").HasContrastObservation, "AC-34：锁定尚未发生对照实验，不能提前归档结果");
        Need(city.SettleDay());
        var rule = s.Rule("hotel-mirror");
        check(rule.HasContrastObservation && rule.ArchivedContrastObservation && rule.ContrastTriggered == true &&
              rule.EvidenceRecords.Any(r => r.Contains("护符 1→0") && r.Contains("心志 12→12")),
            "AC-34：A对照记录实际反噬与护符抵挡，而非把未掉血视作没触发");
        check(s.Item("hotel-clock").Location == ItemLocation.Room && !s.Item("hotel-clock").Discovered &&
              s.Node("hotel").Rooms.First(r => r.HasArchiveCabinet).ArchiveSearchRemaining == 0 && s.Item("seal-a").Charges == 2,
            "AC-34：对照只消耗有限记录柜，不发现合同物、不消费隔离工具");
        check(!city.AddTask(JobKind.ContrastSurvey, "hotel").Success, "AC-34：已取得对照不能重复刷同一实验");

        s = WorldFactory.Create(); city = new CityEngine(s);
        Need(city.AddTask(JobKind.Survey, "hotel")); Day(city);
        var cabinet = s.Node("hotel").Rooms.First(r => r.HasArchiveCabinet);
        cabinet.ArchiveSearchRemaining = 0; cabinet.ContainerLocated = true; // Fixture: an earlier field search consumed the original cabinet.
        Need(city.AddTask(JobKind.ContrastSurvey, "hotel"));
        check(s.Plan[0].SurveyTemplate == "contrast-reserve", "AC-34：原柜已用时明确选取事先登记的另一份有限样本");
        Day(city);
        check(cabinet.ArchiveSearchRemaining == 0 && s.Rule("hotel-mirror").ContrastSamplesRemaining == 0,
            "AC-34：备用来源真实耗尽，原柜未刷新");
    }

    private static void ToolsSupplyAndStorage(Action<bool, string> check)
    {
        var s = WorldFactory.Create(); var city = new CityEngine(s);
        Know(s, "hotel-mirror", "hospital-dark", "source-carry");
        Need(city.AddTask(JobKind.Recovery, "hotel"));
        check(!city.AddTask(JobKind.Recovery, "hospital").Success, "AC-36：同一隔离工具即使余2次也不能同时分给旅馆与医院");
        Need(city.AddTask(JobKind.Recovery, "source"));
        check(s.Plan.Single(t => t.NodeId == "source").ToolIds.SequenceEqual(new[] { "source-case" }),
            "AC-36：源头使用独立专用箱，与旅馆共享的隔离工具没有隐含重复配发");
        Day(city);
        check(s.Item("seal-a").Charges == 1 && s.Item("source-case").Location == ItemLocation.Delivered && s.SourceRecovered,
            "AC-36：旅馆耗隔离1次，专用箱真实随源头交付而不移用于普通物");

        s = WorldFactory.Create(); city = new CityEngine(s);
        check(!city.ScheduleSupply().Success, "AC-36：工具满额不受理补充");
        s.Item("seal-a").Charges = 0;
        Need(city.ScheduleSupply());
        Know(s, "hospital-dark");
        check(!city.AddTask(JobKind.Recovery, "hospital").Success && !city.ScheduleHeadquartersSeal("lamp").Success,
            "AC-36：不能把尚未完成的补充余量用于同日回收或总部封装");
        Need(city.LockDay());
        check(s.Item("seal-a").Charges == 0 && s.Item("seal-a").ReservedBy is not null && s.Item("supply-charge").ReservedBy is not null,
            "AC-36：补充锁定时仅预付并预留工具及唯一耗材，尚未增加次数");
        Need(city.SettleDay());
        check(s.Day == 2 && s.Item("seal-a").Charges == 1 && s.Item("seal-a").AvailableDay == 2 &&
              s.Item("supply-charge").Location == ItemLocation.Consumed && s.Money == 11 && !city.ScheduleSupply().Success,
            "AC-36：补充费用3、日费2，日终+1次次日可派，唯一供应不能重复消费");

        s = WorldFactory.Create(); city = new CityEngine(s);
        var lamp = s.Item("lamp"); lamp.Location = ItemLocation.Warehouse;
        check(!city.SellItem(lamp.Id).Success && !city.InstallLamp(lamp.Id, "hotel").Success,
            "AC-36/37：未封装灯可暂存，但没有现金、安装或支援收益");
        Need(city.ScheduleHeadquartersSeal(lamp.Id)); Need(city.LockDay());
        check(s.Money == 16 && !lamp.Sealed && !city.SellItem(lamp.Id).Success,
            "AC-36：总部封装预付现金0、仍占事务，尚未完成前不能出售");
        Need(city.SettleDay());
        check(lamp.Sealed && lamp.AvailableDay == 2 && s.Item("seal-a").Charges == 1 && s.Money == 14,
            "AC-36：总部补封装消耗真实1次，日终完成并次日生效");
        Need(city.SellItem(lamp.Id));
        check(!city.SellItem(lamp.Id).Success && s.Money == 14, "AC-37：登记出售不提前到账且不能重复登记");
        Day(city);
        check(lamp.Location == ItemLocation.Sold && s.Money == 18 && !city.SellItem(lamp.Id).Success,
            "AC-37：出售日终仅到账一次，实物转已出售状态，不能复制或重卖");
    }

    private static void LampsAndResearch(Action<bool, string> check)
    {
        var s = WorldFactory.Create(); var city = new CityEngine(s);
        var lamp = s.Item("lamp"); lamp.Location = ItemLocation.Warehouse; lamp.Sealed = true;
        var hotel = s.Node("hotel"); hotel.Built = true; hotel.BuiltDay = 0;
        Need(city.AssignStation("hotel", "e5")); Need(city.InstallLamp(lamp.Id, "hotel"));
        check(lamp.Location == ItemLocation.Installed && lamp.InstalledNodeId == "hotel" && !city.SellItem(lamp.Id).Success,
            "AC-37：灯安装为网点实物，不能同时出售或携带");
        Need(city.AddTask(JobKind.Manual, "hospital")); Need(city.AddTask(JobKind.Survey, "source"));
        string assignment = s.Plan[0].Id;
        Need(city.AssignSupport(assignment, lamp.Id));
        check(!city.AssignSupport(s.Plan[1].Id, lamp.Id).Success && !city.UninstallLamp(lamp.Id).Success,
            "AC-37：灯具每日唯一支援预留，不可给两队或带预留拆卸");
        Need(city.LockDay());
        check(city.CanUseLampSupport(s.LockedPlan[0]).Success && lamp.ReservedBy == assignment,
            "AC-37：从当日锁定开始，支援实例正确预留给该项出勤");
        hotel.Invaded = true;
        check(!city.CanUseLampSupport(s.LockedPlan[0]).Success, "AC-37：网点停业立即暂停尚未使用的支援能力");

        s = WorldFactory.Create(); city = new CityEngine(s); lamp = s.Item("lamp"); lamp.Location = ItemLocation.Warehouse; lamp.Sealed = true; lamp.SupportUsedDay = 1;
        s.Node("hotel").Built = true; Need(city.AssignStation("hotel", "e5"));
        Need(city.InstallLamp(lamp.Id, "hotel")); Need(city.UninstallLamp(lamp.Id));
        check(lamp.InstalledNodeId == "" && lamp.SupportUsedDay == 1, "AC-37：拆回仓库清空安装位置但保留实例每日已用额度");
        Need(city.InstallLamp(lamp.Id, "hotel")); Need(city.AddTask(JobKind.Manual, "hospital"));
        check(!city.AssignSupport(s.Plan[0].Id, lamp.Id).Success, "AC-37：同日重新安装不刷新支援额度");

        s = WorldFactory.Create(); city = new CityEngine(s); lamp = s.Item("lamp"); lamp.Location = ItemLocation.Warehouse; lamp.Sealed = true;
        Need(city.ScheduleResearch(lamp.Id)); Need(city.LockDay());
        check(s.Employees.Single(e => e.Id == s.LockedPlan[0].EmployeeIds[0]).Status == EmployeeStatus.Construction && s.LockedPlan.Count == 1 && !city.SellItem(lamp.Id).Success,
            "AC-40：研究占施工状态、1人/1事务并预留唯一目标，不能同时出售");
        Need(city.SettleDay());
        check(lamp.Location == ItemLocation.Consumed && !city.ScheduleResearch(lamp.Id).Success,
            "AC-37/40：研究真实消耗实物，不重刷、不虚构未声明收益");
    }

    private static void ManualLoadoutChanges(Action<bool, string> check)
    {
        var s = WorldFactory.Create(); var city = new CityEngine(s);
        Know(s, "hotel-mirror", "hospital-dark");
        Need(city.AddTask(JobKind.Manual, "hotel"));
        var manual = s.Plan.Single();
        check(manual.ToolIds.SequenceEqual(new[] { "seal-a" }), "M2配装：手操初始自动配发唯一隔离工具");
        Need(city.SetTaskTools(manual.Id, Array.Empty<string>()));
        Need(city.AddTask(JobKind.Recovery, "hospital"));
        var recovery = s.Plan.Single(t => t.Kind == JobKind.Recovery);
        check(manual.ToolIds.Count == 0 && recovery.ToolIds.SequenceEqual(new[] { "seal-a" }),
            "M2配装：明确空配装保留为空，释放唯一工具供同日另一队合法回收");
        int money = s.Money;
        check(!city.SetTaskTools(manual.Id, new[] { "seal-a" }).Success && manual.ToolIds.Count == 0 &&
              recovery.ToolIds.SequenceEqual(new[] { "seal-a" }) && s.Money == money,
            "M2配装：重新加入已占用工具冲突被拒绝并回滚，不影响另一队或资金");
        check(!city.SetTaskTools(recovery.Id, Array.Empty<string>()).Success && recovery.ToolIds.SequenceEqual(new[] { "seal-a" }),
            "M2配装：不能通过手操配装接口移除标准回收的必要工具");
        Need(city.LockDay());
        check(!city.SetTaskTools(manual.Id, new[] { "seal-a" }).Success && !city.SetTaskTools(recovery.Id, Array.Empty<string>()).Success &&
              s.LockedPlan.Single(t => t.Kind == JobKind.Manual).ToolIds.Count == 0 && s.Item("seal-a").ReservedBy == recovery.Id,
            "M2配装：锁定后拒绝重配，已锁定空队和工具预留保持不变");
    }

    private static void BudgetAndFailure(Action<bool, string> check)
    {
        var s = WorldFactory.Create(); var city = new CityEngine(s); s.Money = 28;
        foreach (var id in new[] { "hotel", "depot" }) { s.Node(id).Built = true; s.Node(id).BuiltDay = 0; }
        Need(city.AssignStation("hotel", "e5")); Need(city.AssignStation("depot", "e6"));
        s.Edges.First(e => e.Id == "hq-hotel").Passable = false;
        s.Node("source").SurveyReward = 0; // AC-39 numeric fixture has no additional investigation milestone income.
        Need(city.AddTask(JobKind.Repair, "hotel")); Need(city.AddTask(JobKind.Survey, "source"));
        check(city.GetBudget().ConservativeBalance == 25 && city.GetBudget().UnpaidUpfront == 3 && city.GetBudget().AlreadyPaid == 0,
            "AC-39：准备预测28−抢修2−调查1+营业2−基本2=25");
        Need(city.LockDay());
        check(city.GetBudget().ConservativeBalance == 25 && city.GetBudget().UnpaidUpfront == 0 && city.GetBudget().AlreadyPaid == 3,
            "AC-39：锁定后当前资金已扣预付，预测保持25而非错误22");
        Need(city.SettleDay());
        check(s.Money == 25 && s.LastDaySummary.Count > 0, "AC-39：日终实际25，与准备及锁定预测一致，摘要完整生成");

        s = WorldFactory.Create(); city = new CityEngine(s);
        Need(city.AddTask(JobKind.Manual, "hotel")); Need(city.LockDay());
        var task = s.LockedPlan.Single(); var kit = s.Item("seal-a");
        kit.Location = ItemLocation.Backpack; kit.OwnerId = task.Id; kit.Charges = 1;
        s.Field = new FieldState { AssignmentId = task.Id, NodeId = "hotel", RoomId = "hotel-room1", Finished = true, Escaped = false, BroughtItemIds = new() { kit.Id } };
        Need(city.CompleteManual()); Need(city.SettleDay());
        check(kit.Location == ItemLocation.Room && kit.NodeId == "hotel" && kit.RoomId == "hotel-room1" && kit.Discovered && kit.Charges == 1,
            "AC-40：带入唯一隔离工具也真实遗落且已用次数不退，不免费归库");
        Need(city.AddTask(JobKind.Manual, "hotel")); Need(city.LockDay());
        check(s.LockedPlan.Single().ToolIds.Count == 0, "AC-40：另一可用队伍仍能空手合法重返并取回遗落工具，未形成永久自锁");
    }

    private static void ManagementWitness(bool variantB, Action<bool, string> check)
    {
        var s = WorldFactory.Create(413, variantB); var city = new CityEngine(s);
        Need(city.AddTask(JobKind.Survey, "hotel")); Need(city.AddTask(JobKind.Survey, "source")); Day(city);
        check(s.Money == 14 && s.Item("seal-a").Charges == 2, "AC-42全委派D1：历史+源头调查，余额14，工具2次");
        Need(city.ConfirmRule("source-carry", s.Rule("source-carry").CorrectCandidate));
        Need(city.AddTask(JobKind.ContrastSurvey, "hotel")); Need(city.AddTask(JobKind.Build, "depot")); Day(city);
        check(s.Money == 7 && s.Item("seal-a").Charges == 2 && s.Rule("hotel-mirror").ArchivedContrastObservation,
            "AC-42全委派D2：有限对照+协作建设，余额7，工具2次，观察真实归档");
        Need(city.ConfirmRule("hotel-mirror", s.Rule("hotel-mirror").CorrectCandidate));
        Need(city.AssignStation("depot", "e5"));
        Need(city.AddTask(JobKind.Recovery, "hotel")); Need(city.AddTask(JobKind.Recovery, "source")); Day(city);
        check(s.Money == 16 && s.Item("seal-a").Charges == 1 && s.SourceRecovered,
            "AC-42全委派D3：双回收使用不同收容实例，余额16，工具1次，无现场零钱");
        Need(city.AddTask(JobKind.Build, "hotel")); Day(city);
        check(s.Money == 11 && s.Phase == DayPhase.Preparation, "AC-42全委派D4：旅馆建设+协作营业，余额11，新建当天不交接");
        Need(city.AssignStation("hotel", "e6")); Day(city);
        check(s.Phase == DayPhase.Victory && s.Day == 5 && s.Money == 11 && s.Item("seal-a").Charges == 1 &&
              s.SortieSequence == 0 && !s.EmergencySupportUsed && s.Employees.Count == 6,
            $"AC-42全委派真值{(variantB ? "B" : "A")}：真实有限证据/工具/资金/人力路线第五日11，无补满或额外支援");
    }

    private static void Know(GameState s, params string[] ids)
    { foreach (string id in ids) { var rule = s.Rule(id); rule.EvidenceFound = true; rule.Archived = true; rule.Confirmed = true; } }
    private static void Day(CityEngine city) { Need(city.LockDay()); Need(city.SettleDay()); }
    private static void Need(ActionResult result) { if (!result.Success) throw new InvalidOperationException("M2城市测试前置失败：" + result.Message); }
    private static GameState Clone(GameState s) => JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(s))!;
}
