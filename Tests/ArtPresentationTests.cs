using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Joy.Core;

namespace Joy.Tests;

/// <summary>Public presentation and persisted settlement history must never execute game actions.</summary>
public static class ArtPresentationTests
{
    public static void Run(Action<bool, string> check)
    {
        SettlementAccounting(check);
        EarlyManualReturnHistory(check);
        SettlementRewardsAndEndings(check);
        ItemLegalityAndIdentity(check);
        Reservations(check);
        ReadOnlyAndHiddenTruth(check);
        Persistence(check);
    }

    private static void SettlementAccounting(Action<bool, string> check)
    {
        var state = BudgetFixture();
        var city = new CityEngine(state);
        for (int i = 0; i < 260; i++) state.Record($"旧事件 {i}");
        check(state.Log.Count == 250 && !state.Log.Any(s => s.EndsWith("旧事件 0")) &&
              state.Log[^1].EndsWith("旧事件 259"), "展示日志：到250条上限后淘汰旧记录并保留最新事件");
        check(city.GetBudget().ConservativeBalance == 25, "日结展示账例：28−抢修2−调查1+营业2−支出2预期25");
        Need(city.LockDay());
        check(state.Money == 25 && state.Ledger.Count(l => l.Category == "预付") == 2 &&
              state.Ledger.Where(l => l.Category == "预付").Select(l => l.Amount).Order().SequenceEqual(new[] { -2, -1 }),
            "日结展示：锁定时真实预付两笔共3，不把初始28作为日结起始资金");
        long beforeSequence = state.LogSequence;
        Need(city.SettleDay());
        CheckSevenStages(state, 1, check, "普通日");
        check(state.SettlementTimeline[0].MoneyBefore == 25 && state.SettlementTimeline[^1].MoneyAfter == 25 &&
              state.SettlementTimeline.All(t => t.MoneyBefore == 25 && t.MoneyAfter == 25) && state.Money == 25,
            "日结展示：七阶段连续使用锁定后25，营业2与支出2相抵，不再次扣预付");
        check(state.Ledger.Count(l => l.Category == "营业" && l.Day == 1) == 2 &&
              state.Ledger.Where(l => l.Category == "营业" && l.Day == 1).Sum(l => l.Amount) == 2 &&
              state.Ledger.Count(l => l.Category == "基本支出" && l.Day == 1) == 1 &&
              state.Ledger.Single(l => l.Category == "基本支出" && l.Day == 1).Amount == -2 &&
              28 + state.Ledger.Sum(l => l.Amount) == state.Money,
            "日结展示：两笔营业、一次基本支出与真实台账核对一致");
        check(state.Log.Count == 250 && state.LogSequence > beforeSequence &&
              state.Log[^1].Contains("统一日结完成") &&
              state.SettlementTimeline[0].Events.Any(e => e.Contains("保守调查取得记录")) &&
              state.SettlementTimeline[2].Events.Any(e => e.Contains("相邻中断通路已恢复")) &&
              state.SettlementTimeline.All(t => t.Events.All(e => !e.Contains("旧事件"))),
            "日结展示：日志满250条时仍截取本阶段新调查/抢修事件，不误取旧日志");
        string before = Json(state);
        check(!city.SettleDay().Success && Json(state) == before,
            "日结展示：同一日重复调用结算被拒绝，现金/台账/时间线/随机状态完全不变");
    }

    private static void EarlyManualReturnHistory(Action<bool, string> check)
    {
        var state = WorldFactory.CreateLegacy(); var city = new CityEngine(state);
        Need(city.AddTask(new Assignment { Kind = JobKind.Manual, NodeId = "source", Route = new() { "hq", "hospital", "relay", "source" } }));
        Need(city.LockDay());
        var field = new FieldEngine(state); Need(field.Start(state.LockedPlan.Single()));
        // A deterministic legal test deck drives an actual investigation and confirmation reward.
        state.Field!.HandSlots = new() { CardKind.Investigate, CardKind.Light, CardKind.Search, CardKind.Rest, CardKind.Ward };
        state.Field.Hand = state.Field.HandSlots.OfType<CardKind>().ToList();
        Need(field.Execute(new FieldAction { Kind = FieldActionKind.PlayCard, Card = CardKind.Investigate, HandSlotIndex = 0 }));
        Need(field.Confirm("source-carry", state.Rule("source-carry").CorrectCandidate));
        Need(field.Execute(new FieldAction { Kind = FieldActionKind.Move, TargetId = "source-room1" }));
        Need(field.EndRound());
        Need(field.Execute(new FieldAction { Kind = FieldActionKind.Search }));
        Need(field.Execute(new FieldAction { Kind = FieldActionKind.Move, TargetId = "source-entry" }));
        Need(field.Withdraw());
        check(state.Field.Coins == 1 && state.Item("source-box").Location == ItemLocation.Backpack && !state.Item("source-box").Sealed &&
              !state.Rule("line-seal").Confirmed && !state.Rule("line-seal").Observed,
            "提前归队账例：真实确认获得1零钱、未封装源头在包中，南线运输仍未知");
        var direct = JsonSerializer.Deserialize<GameState>(Json(state))!;
        for (int i = 0; i < 260; i++) state.Record($"归队前旧事件 {i}");
        int beforeReturn = state.Money;
        Need(city.CompleteManual());
        int returnedMoney = state.Money;
        check(returnedMoney == beforeReturn + 1 && state.Node("hospital").Invaded && state.Rule("line-seal").Observed &&
              state.Item("source-box").Location == ItemLocation.Warehouse && !state.Item("source-box").Sealed,
            "提前归队：未知运输真实侵入医院，实物暂存，零钱立即且仅入账1");
        var returnRecords = state.LastDaySummary.Where(s => s.StartsWith("归队流水：")).ToArray();
        check(state.Log.Count == 250 && returnRecords.Length == 2 &&
              returnRecords[0].Contains("实际运输后果") && returnRecords[0].Contains(state.Node("hospital").Name) &&
              returnRecords[1].Contains("现场零钱入账") && returnRecords[1].Contains("到账 1") &&
              returnRecords.All(s => !s.Contains("旧事件")),
            "提前归队：250日志上限下按序保存本次真实运输与零钱流水，不带入旧记录");
        string completed = Json(state);
        Need(city.CompleteManual());
        check(Json(state) == completed, "提前归队：再次CompleteManual不追加流水、不重复支付、不改变任何状态");
        for (int i = 0; i < 260; i++) state.Record($"归队后其他记录 {i}");
        check(!state.Log.Any(s => s.Contains("实际运输后果") || s.Contains("现场零钱入账")),
            "提前归队：回归明确覆盖原运输与零钱日志已被250条上限淘汰的情况");
        Need(city.SettleDay());
        CheckSevenStages(state, 1, check, "提前手操归队日");
        var first = state.SettlementTimeline[0];
        check(returnRecords.All(s => first.Events.Count(e => e == s) == 1) &&
              first.Events.All(e => !e.Contains("旧事件") && !e.Contains("其他记录")),
            "提前归队：日结第一阶段保留早前真实运输与零钱明细各一次，即使原日志已淘汰");
        check(first.MoneyBefore == returnedMoney && first.MoneyAfter == returnedMoney &&
              state.Money == returnedMoney + state.Node("source").SurveyReward - state.DailyExpense,
            "提前归队：第一阶段从真实日结余额起记，先前零钱不造成第二次余额增长");
        check(state.Ledger.Count(e => e.Category == "现场零钱") == 1 &&
              state.Ledger.Single(e => e.Category == "现场零钱").Amount == 1 &&
              state.Node("hospital").Invaded && !state.SourceRecovered,
            "提前归队：零钱台账仅一笔，日结保留实际入侵且不把未封装源头伪作已交付");
        var directCity = new CityEngine(direct); Need(directCity.SettleDay());
        check(direct.SettlementTimeline[0].Events.Count(e => e.Contains("实际运输后果")) == 1 &&
              direct.SettlementTimeline[0].Events.Count(e => e.Contains("现场零钱入账")) == 1 &&
              direct.SettlementTimeline[0].MoneyAfter - direct.SettlementTimeline[0].MoneyBefore == 1 &&
              direct.Money == state.Money && direct.Ledger.Count(e => e.Category == "现场零钱") == 1,
            "日结内归队：同一实际结果的raw日志和归队流水不重复展示，零钱仍只支付一次");
        completed = Json(state);
        check(!city.SettleDay().Success && Json(state) == completed,
            "提前归队：再次日结不重放运输、流水或零钱入账");
    }

    private static void SettlementRewardsAndEndings(Action<bool, string> check)
    {
        var state = WorldFactory.Create();
        var city = new CityEngine(state);
        var mirror = state.Rule("hotel-mirror");
        mirror.EvidenceFound = mirror.Archived = mirror.Confirmed = true;
        var lamp = state.Item("lamp"); lamp.Location = ItemLocation.Warehouse; lamp.Sealed = true;
        Need(city.SellItem(lamp.Id));
        Need(city.AddTask(JobKind.Recovery, "hotel"));
        Need(city.LockDay());
        Need(city.SettleDay());
        CheckSevenStages(state, 1, check, "交付与出售日");
        check(state.SettlementTimeline.Select(t => t.MoneyAfter - t.MoneyBefore)
                  .SequenceEqual(new[] { 0, 4, 6, -2, 0, 0, 0 }) &&
              state.SettlementTimeline[0].MoneyBefore == 15 && state.Money == 23,
            "日结展示：合同+4、处置+6、支出−2分属真实阶段，顺序金额连续");
        check(state.Item("hotel-clock").Location == ItemLocation.Delivered && lamp.Location == ItemLocation.Sold &&
              state.Item("seal-a").Charges == 1 &&
              state.SettlementTimeline[0].Events.Any(e => e.Contains("回收归队")) &&
              state.SettlementTimeline[2].Events.Any(e => e.Contains("出售")),
            "日结展示：归队消耗、合同实物与出售状态来自真实执行结果");

        var deadline = WorldFactory.Create(); deadline.Day = deadline.Deadline;
        var deadlineCity = new CityEngine(deadline);
        Need(deadlineCity.LockDay()); Need(deadlineCity.SettleDay());
        CheckSevenStages(deadline, 5, check, "期限失败日");
        check(deadline.Phase == DayPhase.Failed && deadline.Day == 5 &&
              deadline.SettlementTimeline[5].Events.Any(e => e.Contains("期限已到")) &&
              deadline.SettlementTimeline[6].Events.Any(e => e.Contains("未额外开放经营日")),
            "日结展示：期限失败保留七阶段且不伪造下一经营日");

        var victory = WorldFactory.Create(); var victoryCity = new CityEngine(victory);
        BuildStationFixture(victory, victoryCity);
        victory.SourceRecovered = true;
        Need(victoryCity.LockDay()); Need(victoryCity.SettleDay());
        CheckSevenStages(victory, 1, check, "交接日");
        check(victory.Phase == DayPhase.Victory && victory.Day == 1 &&
              victory.SettlementTimeline[5].Events.Any(e => e.Contains("单城验证完成")) &&
              victory.SettlementTimeline[6].Events.Any(e => e.Contains("未额外开放经营日")),
            "日结展示：达成交接后保留真实结束阶段，不额外发营业收入或推进日期");
    }

    private static void ItemLegalityAndIdentity(Action<bool, string> check)
    {
        var state = WorldFactory.Create(); var city = new CityEngine(state);
        var lamp = state.Item("lamp"); lamp.Location = ItemLocation.Warehouse;
        var unsealed = ItemPresentation.Describe(state, lamp);
        string before = Json(state);
        check(unsealed.ArtId == "item-lamp" && unsealed.Seal == "未封装" &&
              unsealed.Support.Contains("不能出售或支援") && !unsealed.Support.Contains("公开报价") &&
              !unsealed.Support.Contains("今日支援未用"),
            "实物展示：未封装灯只显示暂存和禁用，不呈现可出售报价或可用支援");
        check(!city.SellItem(lamp.Id).Success && !city.InstallLamp(lamp.Id, "hotel").Success && Json(state) == before,
            "实物展示：未封装灯的出售/安装拒绝与提示一致，拒绝不变更状态");

        lamp.Sealed = true; lamp.SupportUsedDay = state.Day;
        state.Node("hotel").Built = true;
        Need(city.AssignStation("hotel", "e5"));
        Need(city.InstallLamp(lamp.Id, "hotel"));
        var installed = ItemPresentation.Describe(state, lamp);
        check(installed.Location.Contains(state.Node("hotel").Name) && installed.Support.Contains("今日支援已用"),
            "实物展示：安装地点和该实例今日已用额度同时显示");
        Need(city.UninstallLamp(lamp.Id)); Need(city.InstallLamp(lamp.Id, "hotel"));
        Need(city.AddTask(JobKind.Manual, "hospital"));
        check(lamp.SupportUsedDay == state.Day && ItemPresentation.Describe(state, lamp).Support.Contains("今日支援已用") &&
              !city.AssignSupport(state.Plan.Single().Id, lamp.Id).Success,
            "实物展示：同日拆装仍显示已用，不能刷新或再次预留支援");

        var clock = state.Item("hotel-clock"); clock.Location = ItemLocation.Warehouse; clock.Sealed = true;
        before = Json(state);
        check(ItemPresentation.Describe(state, clock).ArtId == "item-clock" &&
              ItemPresentation.Describe(state, clock).Support == "" &&
              !city.InstallLamp(clock.Id, "hotel").Success && Json(state) == before,
            "实物展示：座钟维持独立主体且没有灯具装配/支援能力");
        bool stableIdentity = true;
        foreach (var location in new[] { ItemLocation.Room, ItemLocation.Backpack, ItemLocation.Warehouse, ItemLocation.Delivered })
        {
            clock.Location = location;
            stableIdentity &= ItemPresentation.Describe(state, clock).ArtId == "item-clock";
        }
        check(stableIdentity, "实物展示：座钟发现、携带、归库、交付跨状态保持同一美术ID");

        var saleState = WorldFactory.Create(); var saleCity = new CityEngine(saleState);
        var saleLamp = saleState.Item("lamp"); saleLamp.Location = ItemLocation.Warehouse; saleLamp.Sealed = true;
        int money = saleState.Money;
        Need(saleCity.SellItem(saleLamp.Id));
        check(ItemPresentation.Describe(saleState, saleLamp).Support.Contains("尚未到账") && saleState.Money == money,
            "实物展示：登记出售显示尚未到账，不把报价当当前现金");
    }

    private static void Reservations(Action<bool, string> check)
    {
        var state = WorldFactory.Create(); var city = new CityEngine(state);
        var rule = state.Rule("hotel-mirror"); rule.Archived = rule.Confirmed = rule.EvidenceFound = true;
        Need(city.AddTask(JobKind.Recovery, "hotel"));
        Need(city.AddTask(JobKind.Survey, "source"));
        var recovery = state.Plan.Single(t => t.Kind == JobKind.Recovery);
        string planned = ItemPresentation.Describe(state, state.Item("seal-a")).Reservation;
        check(planned.Contains(state.Node("hotel").Name) && !planned.Contains(state.Node("source").Name) &&
              planned.Contains(recovery.EmployeeIds.Count + " 人") && state.Item("seal-a").Charges == 2,
            "预留展示：准备期唯一隔离工具标注实际旅馆事务与人数，预留不消耗次数");
        Need(city.LockDay());
        var task = state.LockedPlan.Single(t => t.Kind == JobKind.Recovery);
        check(state.Item("seal-a").ReservedBy == task.Id &&
              ItemPresentation.Describe(state, state.Item("seal-a")).Reservation.Contains(state.Node(task.NodeId).Name),
            "预留展示：锁定工具标签与真实ReservedBy指向同一任务");
        check(state.Item("hotel-clock").ReservedBy == task.Id &&
              ItemPresentation.Describe(state, state.Item("hotel-clock")).Reservation.Contains(state.Node(task.NodeId).Name) &&
              !ItemPresentation.Describe(state, state.Item("hotel-clock")).Reservation.Contains("未被"),
            "预留展示：现场合同目标虽非ToolIds/TargetItemId，仍按真实ReservedBy显示锁定归属");

        state = WorldFactory.Create(); city = new CityEngine(state);
        var lamp = state.Item("lamp"); lamp.Location = ItemLocation.Warehouse;
        Need(city.ScheduleHeadquartersSeal(lamp.Id)); Need(city.LockDay());
        var sealing = state.LockedPlan.Single();
        var kitDisplay = ItemPresentation.Describe(state, state.Item("seal-a"));
        var targetDisplay = ItemPresentation.Describe(state, lamp);
        check(state.Item("seal-a").ReservedBy == sealing.Id && lamp.ReservedBy == sealing.Id &&
              kitDisplay.Reservation == targetDisplay.Reservation && targetDisplay.Reservation.Contains(state.Node("hq").Name) &&
              kitDisplay.Capacity.Contains("剩余 2 / 容量 2"),
            "预留展示：总部封装的工具与目标实物属于同一真实事务，当前余量不提前扣除");

        state = WorldFactory.Create(); city = new CityEngine(state);
        lamp = state.Item("lamp"); lamp.Location = ItemLocation.Warehouse; lamp.Sealed = true;
        state.Node("hotel").Built = true; Need(city.AssignStation("hotel", "e5"));
        Need(city.InstallLamp(lamp.Id, "hotel")); Need(city.AddTask(JobKind.Manual, "hospital"));
        Need(city.AssignSupport(state.Plan.Single().Id, lamp.Id)); Need(city.LockDay());
        var support = ItemPresentation.Describe(state, lamp);
        check(support.Location.Contains(state.Node("hotel").Name) &&
              support.Reservation.Contains(state.Node("hospital").Name) && lamp.ReservedBy == state.LockedPlan.Single().Id,
            "预留展示：灯具实体留在旅馆，唯一支援归属医院出勤，不混淆所在地和任务归属");
    }

    private static void ReadOnlyAndHiddenTruth(Action<bool, string> check)
    {
        var state = BudgetFixture(); var city = new CityEngine(state);
        Need(city.LockDay()); Need(city.SettleDay());
        string before = Json(state); ulong randomBefore = state.RandomState;
        for (int visit = 0; visit < 8; visit++)
        {
            foreach (var item in state.Items) _ = ItemPresentation.Describe(state, item);
            _ = city.GetBudget();
            _ = state.SettlementTimeline.Select(t => $"{t.Title}:{t.MoneyBefore}:{t.MoneyAfter}:{string.Join("；", t.Events)}").ToArray();
        }
        check(Json(state) == before && state.RandomState == randomBefore,
            "只读展示：重复查看实物/预算/时间线不推进日期、支付、余量、日志、随机状态或任何字段");
        var other = JsonSerializer.Deserialize<GameState>(before)!;
        other.Rule("hotel-mirror").MirrorVariantB = !state.Rule("hotel-mirror").MirrorVariantB;
        other.Rule("hotel-mirror").CorrectCandidate = 1 - state.Rule("hotel-mirror").CorrectCandidate;
        check(state.Items.Select(i => ItemPresentation.Describe(state, i))
                  .SequenceEqual(other.Items.Select(i => ItemPresentation.Describe(other, i))),
            "隐藏信息配对：公开状态相同的A/B真值不改变实物展示文字或美术ID");
    }

    private static void Persistence(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "JoyArtPresentationTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var state = BudgetFixture(); var city = new CityEngine(state);
            Need(city.LockDay()); Need(city.SettleDay());
            var store = new SaveStore(Path.Combine(root, "timeline"));
            Need(store.Save(state));
            var loaded = store.Load();
            check(loaded.Success && Json(loaded.State!) == Json(state),
                "时间线存档：七阶段金额/事件/LogSequence随v2完整往返");
            if (!loaded.Success) throw new InvalidOperationException(loaded.Message);
            var restored = loaded.State!;
            string before = Json(restored);
            foreach (var item in restored.Items) _ = ItemPresentation.Describe(restored, item);
            check(!new CityEngine(restored).SettleDay().Success && Json(restored) == before &&
                  restored.Money == 25 && restored.PaidRewards.SequenceEqual(state.PaidRewards),
                "时间线存档：读档后查看与重复结算不重播营业、预付或奖励");
            Need(new SaveStore(Path.Combine(root, "timeline-resaved")).Save(restored));
            check(restored.Money == 25 && restored.Ledger.Count == state.Ledger.Count,
                "时间线存档：再次保存只建立检查点，不执行收入或日结");

            var oldStore = new SaveStore(Path.Combine(root, "old-v2"));
            Directory.CreateDirectory(Path.GetDirectoryName(oldStore.SavePath)!);
            var oldPayload = JsonNode.Parse(Json(state))!.AsObject();
            oldPayload.Remove("SettlementTimeline"); oldPayload.Remove("LogSequence");
            string payload = oldPayload.ToJsonString();
            File.WriteAllText(oldStore.SavePath, Json(new
            {
                FormatVersion = 2, Payload = payload,
                Sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)))
            }));
            var oldLoaded = oldStore.Load();
            check(oldLoaded.Success && oldLoaded.State!.SettlementTimeline.Count == 0 && oldLoaded.State.LogSequence == 0 &&
                  oldLoaded.State.Money == state.Money && oldLoaded.State.Ledger.Count == state.Ledger.Count,
                "旧v2兼容：缺少Timeline/LogSequence时默认空记录，不迁移或重算收益");
            if (!oldLoaded.Success) throw new InvalidOperationException(oldLoaded.Message);
            var oldState = oldLoaded.State!;
            before = Json(oldState);
            foreach (var item in oldState.Items) _ = ItemPresentation.Describe(oldState, item);
            check(!new CityEngine(oldState).SettleDay().Success && Json(oldState) == before,
                "旧v2兼容：无时间线的已结算档仍禁止重结，不补造历史收入");
            Need(new SaveStore(Path.Combine(root, "old-v2-resaved")).Save(oldState));
            var nextCity = new CityEngine(oldState);
            Need(nextCity.LockDay()); Need(nextCity.SettleDay());
            check(oldState.SettlementTimeline.Count == 7 && oldState.LogSequence > 0 &&
                  oldState.SettlementTimeline.All(t => t.Day == 2) &&
                  oldState.SettlementTimeline[6].Events.Any(e => e.Contains("统一日结完成")),
                "旧v2兼容：下一真实经营日生成新七阶段记录，旧日志数量不干扰新事件识别");
        }
        finally
        {
            string resolved = Path.GetFullPath(root);
            string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (resolved.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileName(resolved).StartsWith("JoyArtPresentationTests-", StringComparison.Ordinal) && Directory.Exists(resolved))
                Directory.Delete(resolved, recursive: true);
        }
    }

    private static GameState BudgetFixture()
    {
        var state = WorldFactory.Create(); state.Money = 28;
        var city = new CityEngine(state);
        BuildStationFixture(state, city);
        state.Edges.Single(e => e.Id == "hq-hotel").Passable = false;
        state.Node("source").SurveyReward = 0;
        Need(city.AddTask(JobKind.Repair, "hotel")); Need(city.AddTask(JobKind.Survey, "source"));
        return state;
    }

    private static void BuildStationFixture(GameState state, CityEngine city)
    {
        foreach (string id in new[] { "hotel", "depot" }) { state.Node(id).Built = true; state.Node(id).BuiltDay = 0; }
        Need(city.AssignStation("hotel", "e5")); Need(city.AssignStation("depot", "e6"));
    }

    private static void CheckSevenStages(GameState state, int day, Action<bool, string> check, string label)
    {
        string[] titles = { "归队、真实消耗与运输", "证据归档与合同交付", "工程与处置完成",
            "晨间资格收入与必要费用", "共同快照扩张与封控", "交接、失败与期限检查", "实际进入的新阶段" };
        check(state.SettlementTimeline.Select(t => t.Sequence).SequenceEqual(Enumerable.Range(1, 7)) &&
              state.SettlementTimeline.Select(t => t.Title).SequenceEqual(titles) &&
              state.SettlementTimeline.All(t => t.Day == day && t.Events.Count > 0),
            $"日结展示（{label}）：七阶段按需求顺序保留当日事件，没有空白占位阶段");
        check(state.SettlementTimeline.Zip(state.SettlementTimeline.Skip(1)).All(p => p.First.MoneyAfter == p.Second.MoneyBefore),
            $"日结展示（{label}）：相邻阶段金额连续，没有动画生成的虚构资金变化");
    }

    private static string Json<T>(T value) => JsonSerializer.Serialize(value);
    private static void Need(ActionResult result)
    {
        if (!result.Success) throw new InvalidOperationException("美术展示测试前置失败：" + result.Message);
    }
}
