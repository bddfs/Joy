using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Joy.Core;

namespace Joy.Tests;

/// <summary>Requirements v0.3, planning-v0.2 alignment: new M2 behavior, separate from legacy smoke checks.</summary>
public static class M2FieldTests
{
    public static void Run(Action<bool, string> check)
    {
        CheckExperiment(check);
        CheckCountersAndMimic(check);
        CheckPreviews(check);
        CheckQuotaAndSlots(check);
        CheckLossAndSupport(check);
        CheckContinuation(check);
    }

    private static void CheckExperiment(Action<bool, string> check)
    {
        var (a, ea) = Start(false);
        var (b, eb) = Start(true);
        var ar = a.Rule("hotel-mirror"); var br = b.Rule("hotel-mirror");
        check(ar.EvidenceFound && br.EvidenceFound && !ar.Archived && !br.Archived && ar.Clue == br.Clue,
            "M2 AC-34：同一历史录像进入现场可用但不提前归档，A/B 初始证据相同");
        check(a.Field!.NormalSearchCounts.Count == 0 && b.Field!.NormalSearchCounts.Count == 0,
            "M2 AC-34：旧出勤的第二次搜查记录不计入本次行为次数");
        Need(ea.Execute(Move("hotel-room1"))); Need(eb.Execute(Move("hotel-room1")));
        var search = new FieldAction { Kind = FieldActionKind.Search };
        check(ea.Preview(search) == eb.Preview(search) && !ea.Preview(search).RequiresConfirmation,
            "M2 AC-26/38：首次暗搜的预览、启用、费用和危险确认不泄露 A/B 真值");
        Need(ea.Execute(search)); Need(eb.Execute(search));
        check(a.Field.Will == 12 && a.Field.Wards == 0 && b.Field!.Will == 12 && b.Field.Wards == 1,
            "M2 AC-34：首次暗处正常搜查，A 反噬被护符挡住，B 无反噬");
        check(ar.Observed && br.Observed && ar.HasContrastObservation && br.HasContrastObservation && ar.ContrastTriggered == true && br.ContrastTriggered == false,
            "M2 AC-34：掉血与触发分别记录，无反噬也留下可用观察事实");
        check(a.Log.Any(l => l.Contains("出现反噬")) && a.Log.Any(l => l.Contains("消耗 1 护符")) && b.Log.Any(l => l.Contains("未观察到反噬")),
            "M2 AC-34：日志明确实际反噬、护符抵挡和未观察到的事实");
        check(!a.Log.Any(l => l.Contains(ar.Answer) || l.Contains(ar.Id)) && !b.Log.Any(l => l.Contains(br.Answer) || l.Contains(br.Id)),
            "M2 AC-34：确认前实际日志不显示隐藏规则 ID、完整条件或真值");
        foreach (var state in new[] { a, b })
        {
            var room = state.Node("hotel").Rooms.First(r => r.Id == "hotel-room1");
            check(room.ArchiveSearchRemaining == 0 && room.ContainerLocated && !state.Item("hotel-clock").Discovered && state.Item("hotel-clock").Location == ItemLocation.Room,
                "M2 AC-34：第一档案柜只定位第二容器，合同物未发现且未入包");
        }
        int actions = a.Field.Actions;
        check(!ea.Execute(new FieldAction { Kind = FieldActionKind.Pickup, TargetId = "hotel-clock" }).Success && a.Field.Actions == actions,
            "M2 AC-34：已定位容器不能通过拾取免费获得尚未搜查的合同物");
        Need(ea.EndRound()); Need(eb.EndRound());
        a.Field.Light = true; b.Field!.Light = true; a.Field.Wards = 0; b.Field.Wards = 0;
        Need(ea.Execute(search)); Need(eb.Execute(search));
        check(a.Field.Will == 12 && b.Field.Will == 10,
            "M2 AC-34：第二次正常搜查时，照明只豁免 A，不能豁免 B");
        check(a.Item("hotel-clock").Location == ItemLocation.Backpack && b.Item("hotel-clock").Location == ItemLocation.Backpack && a.Field.NormalSearchCounts["hotel-room1"] == 2,
            "M2 AC-34：第二容器真实消耗一次搜查并转移唯一合同物");
    }

    private static void CheckCountersAndMimic(Action<bool, string> check)
    {
        var (state, engine) = Start(true);
        Need(engine.Execute(Move("hotel-room1")));
        Need(engine.Execute(new FieldAction { Kind = FieldActionKind.Search }));
        check(state.Field!.MimicReference is null, "M2 AC-35：徒手试探不建立模仿参考");
        Need(engine.EndRound());
        Need(engine.Execute(Move("hotel-room2")));
        SetHand(state, CardKind.Search, CardKind.Mimic, CardKind.Sprint);
        Need(engine.Execute(Play(CardKind.Search, 0)));
        Need(engine.Execute(Move("hotel-room1")));
        Need(engine.EndRound());
        SetHand(state, CardKind.Mimic, CardKind.Search, CardKind.Light);
        int will = state.Field.Will;
        Need(engine.Execute(Play(CardKind.Mimic, 0)));
        check(state.Field.NormalSearchCounts["hotel-room1"] == 1 && state.Field.NormalSearchCounts["hotel-room2"] == 1 && state.Field.Will == will,
            "M2 AC-35：不同房间独立计数，离房换轮不重置，模仿不增加 B 的正常次数");
        check(state.Item("hotel-clock").Location == ItemLocation.Backpack && state.Field.Exhausted.Contains(CardKind.Mimic),
            "M2 AC-35：模仿仍消耗当前真实库存并进入出勤耗尽");
        check(!engine.Execute(Play(CardKind.Search, 1)).Success,
            "M2 AC-34：两处容器用尽不能因模仿或返回刷新库存");
        Need(engine.Execute(Move("hotel-entry"))); Need(engine.Withdraw());
        Need(new CityEngine(state).CompleteManual());
        state.Field = null;
        var revisit = new Assignment { Id = "revisit", Kind = JobKind.Manual, NodeId = "hotel" };
        Need(engine.Start(revisit));
        check(state.Field!.NormalSearchCounts.Count == 0 && state.Field.MimicReference is null && state.Field.Exhausted.Count == 0,
            "M2 AC-35：新出勤重置正常次数、模仿参考与耗尽");
        check(state.Node("hotel").Rooms.First(r => r.Id == "hotel-room1").ArchiveSearchRemaining == 0,
            "M2 AC-34：新出勤不补充地点档案柜库存");

        (state, engine) = Start(true);
        Need(engine.Execute(Move("hotel-room1"))); Need(engine.Execute(Move("hotel-room2")));
        SetHand(state, CardKind.Search);
        Need(engine.Execute(Play(CardKind.Search, 0)));
        Need(engine.EndRound());
        SetHand(state, CardKind.Sprint, CardKind.Mimic);
        Need(engine.Execute(new FieldAction { Kind = FieldActionKind.PlayCard, Card = CardKind.Sprint, HandSlotIndex = 0, TargetId = "hotel-room1" }));
        Need(engine.Execute(Play(CardKind.Mimic, 1)));
        check(state.Field!.NormalSearchCounts.GetValueOrDefault("hotel-room1") == 0 && !state.Rule("hotel-mirror").HasContrastObservation,
            "M2 AC-34：模仿无伤不能单独形成区分 A/B 的对照观察");
        check(state.Node("hotel").Rooms.First(r => r.Id == "hotel-room1").ArchiveSearchRemaining == 0 && !state.Item("hotel-clock").Discovered,
            "M2 AC-34：模仿第一柜也只取得定位记录，不复制旧房物品");
    }

    private static void CheckPreviews(Action<bool, string> check)
    {
        var (state, engine) = Start(false);
        Need(engine.Execute(Move("hotel-room1")));
        var rule = state.Rule("hotel-mirror"); rule.Confirmed = true;
        state.Field!.Light = true;
        var search = new FieldAction { Kind = FieldActionKind.Search };
        var preview = engine.Preview(search);
        check(new[] { "实际动作 / 对象 / 费用", "基础效果", "已知会触发", "相关已知不触发", "尚不确定" }.All(preview.Text.Contains),
            "M2 AC-38：常驻行动预览包含五个明确分区");
        check(Section(preview.Text, "已知会触发", "相关已知不触发").Contains("本步没有") && Section(preview.Text, "相关已知不触发", "尚不确定").Contains("已有照明"),
            "M2 AC-38：已知 A 在亮灯搜查时只列相关豁免，不混入触发后果");
        state.Field.Light = false; state.Field.Wards = 0; state.Field.Will = 2;
        check(engine.Preview(search).RequiresConfirmation && engine.PreviewDanger(search),
            "M2 AC-38：公开确定立即失败才标记危险确认");
        rule.Confirmed = false;
        check(!engine.Preview(search).RequiresConfirmation && !engine.PreviewDanger(search),
            "M2 AC-26：相同致死隐藏真值不能暗中触发危险确认");

        (state, engine) = Start(true);
        Need(engine.Execute(Move("hotel-room1"))); Need(engine.Execute(search)); Need(engine.EndRound());
        rule = state.Rule("hotel-mirror"); rule.Confirmed = true;
        state.Field!.Light = true; state.Field.Wards = 0;
        preview = engine.Preview(search);
        check(Section(preview.Text, "已知会触发", "相关已知不触发").Contains(rule.Answer),
            "M2 AC-38：已知 B 第二次搜查即使亮灯也列在会触发区");
        state.Rules.Add(new RuleState { Id = "irrelevant", NodeId = "hotel", Confirmed = true, Trigger = RuleTrigger.Knock, Answer = "仅敲门相关的禁令" });
        check(!engine.Preview(search).Text.Contains("仅敲门相关的禁令"),
            "M2 AC-38：搜查预览不泛列无关敲门禁令");
        SetHand(state, CardKind.Mimic); state.Field.MimicReference = CardKind.Search;
        preview = engine.Preview(Play(CardKind.Mimic, 0));
        check(Section(preview.Text, "相关已知不触发", "尚不确定").Contains("模仿不产生正常搜查行为"),
            "M2 AC-38：模仿的已知行为豁免与真实库存结果分开显示");

        (state, engine) = Start(false);
        Need(engine.Execute(Move("hotel-room1")));
        state.Field!.Wards = 1;
        state.Rules.Add(new RuleState { Id = "known", NodeId = "hotel", Scope = RuleScope.Local, Order = 10, Confirmed = true, Trigger = RuleTrigger.SearchInDark, WillDamage = 3, Wardable = true, Answer = "已知暗搜代价" });
        preview = engine.Preview(search);
        check(preview.Text.Contains("共用护符") && !preview.Text.Contains("按本步公开条件预计") && !preview.RequiresConfirmation,
            "M2 AC-26/38：已知与未知可能竞争护符时只给顺序及条件结果，不伪造净损失");
        state.Field.Light = true;
        state.Rules.Add(new RuleState { Id = "known-end", NodeId = "hotel", Scope = RuleScope.Local, Confirmed = true, Trigger = RuleTrigger.LightEnds, WillDamage = 3, Wardable = true, Answer = "已知熄灯代价" });
        preview = engine.PreviewEndRound();
        check(!preview.Text.Contains("预计轮末心志") && preview.Text.Contains("未知项可能先消耗同一护符"),
            "M2 AC-27：轮末未知竞争不显示确定的剩余护符与净损失");
        state.Field.Light = false; state.Field.Disturbance = 7; state.Field.Will = 3;
        check(engine.PreviewEndRound().RequiresConfirmation,
            "M2 AC-38：未点灯且阈值袭击确定致死时，根据公开轮末事实要求确认");
        state.Field.Round = state.Field.RoundLimit;
        check(engine.PreviewEndRound().RequiresConfirmation, "M2 AC-38：公开封门期限触发必要确认，与隐藏真值无关");
    }

    private static void CheckQuotaAndSlots(Action<bool, string> check)
    {
        var (state, engine) = Start(false);
        var rule = state.Rule("hotel-mirror"); rule.LastCityAttemptDay = state.Day;
        check(!engine.Confirm(rule.Id, 0).Success && state.Field!.LastConfirmationRound == 0 && state.Field.Disturbance == 0,
            "M2 AC-35：公司当日已判断的同条现场不能重交，也不追加异动或消耗别条额度");
        state.Rules.Add(new RuleState { Id = "other", NodeId = "hotel", EvidenceFound = true, Candidates = new() { "甲", "乙", "丙" }, CorrectCandidate = 0, Answer = "甲" });
        Need(engine.Confirm("other", 0));
        check(state.Rule("other").Confirmed && state.Field!.LastConfirmationRound == state.Field.Round,
            "M2 AC-35：另一条规则仍可使用全现场本轮一次额度");
        check(state.Field!.HandSlots.Count == 5 && state.Field.HandSlots.All(c => c.HasValue), "M2 AC-38：轮初生成固定五个卡槽");
        SetHand(state, CardKind.Ward, CardKind.Light, CardKind.Rest, CardKind.Sprint, CardKind.Mimic);
        var slots = state.Field.HandSlots.ToList();
        Need(engine.Execute(Play(CardKind.Light, 1)));
        check(state.Field.HandSlots[1] is null && Enumerable.Range(0, 5).Where(i => i != 1).All(i => state.Field.HandSlots[i] == slots[i]) && state.Field.Hand.Count == 4,
            "M2 AC-38：出牌只清空原槽，其他卡位置稳定，兼容手牌集合只移除已用牌");
        int actions = state.Field.Actions;
        check(!engine.Execute(Play(CardKind.Ward, 1)).Success && state.Field.Actions == actions,
            "M2 AC-38：空槽不会借用其他位置的卡或扣费");
        Need(engine.EndRound());
        check(state.Field.HandSlots.Count == 5 && state.Field.HandSlots.All(c => c.HasValue) && state.Field.Hand.SequenceEqual(state.Field.HandSlots.Select(c => c!.Value)),
            "M2 AC-38：换轮才按新抽牌顺序重新填充五槽");
    }

    private static void CheckLossAndSupport(Action<bool, string> check)
    {
        var (state, engine) = Start(false);
        Need(engine.Execute(Move("hotel-room1"))); Need(engine.Execute(new FieldAction { Kind = FieldActionKind.Search })); Need(engine.EndRound());
        SetHand(state, CardKind.Search, CardKind.Seal);
        state.Field!.Light = true;
        Need(engine.Execute(Play(CardKind.Search, 0))); Need(engine.Execute(new FieldAction { Kind = FieldActionKind.PlayCard, Card = CardKind.Seal, HandSlotIndex = 1, TargetId = "hotel-clock" }));
        state.Field.Will = 3; state.Field.Wards = 0; state.Field.Disturbance = 7;
        Need(engine.EndRound());
        check(state.Field.Finished && !state.Field.Escaped && state.Item("seal-a").Location == ItemLocation.Room && state.Item("seal-a").Charges == 1 && state.Item("seal-a").RoomId == "hotel-room1",
            "M2 AC-40：封装后失败，带入隔离工具遗落且已消耗次数不退");
        check(state.Item("hotel-clock").Location == ItemLocation.Room && state.Item("hotel-clock").Sealed && state.Item("hotel-clock").Discovered,
            "M2 AC-40：失败合同物保留已封装唯一实物与合法拾取状态");
        Need(new CityEngine(state).CompleteManual());
        check(state.Item("seal-a").Location == ItemLocation.Room, "M2 AC-40：城市失败结转不把遗落的带入装备自动送回仓库");
        state.Field = null;
        Need(engine.Start(new Assignment { Id = "return-no-kit", Kind = JobKind.Manual, NodeId = "hotel" }));
        Need(engine.Execute(Move("hotel-room1")));
        Need(engine.Execute(new FieldAction { Kind = FieldActionKind.Pickup, TargetId = "seal-a" }));
        check(state.Item("seal-a").Charges == 1 && state.Item("seal-a").Location == ItemLocation.Backpack,
            "M2 AC-40：可以不带工具重返现场，取回同一件已部分消耗的工具");

        (state, engine) = Start(false);
        var station = state.Node("depot"); station.Built = true; station.BuiltDay = 0; state.Stations[station.Id] = "e6";
        var lamp = state.Item("lamp"); lamp.Location = ItemLocation.Installed; lamp.InstalledNodeId = station.Id; lamp.Sealed = true; lamp.ReservedBy = state.Field!.AssignmentId;
        state.Field.AssignedLightSupportId = lamp.Id; state.Field.Disturbance = 2; state.Field.MimicReference = CardKind.Search; state.Field.Actions = 0;
        var support = new FieldAction { Kind = FieldActionKind.SupportLight };
        check(engine.Preview(support).Allowed && engine.Preview(support).Cost == 0, "M2 AC-37：已分配灯具支援在零行动时仍可使用");
        Need(engine.Execute(support));
        check(state.Field.Light && state.Field.Disturbance == 1 && state.Field.Actions == 0 && state.Field.MimicReference == CardKind.Search,
            "M2 AC-37：网点免费点灯产生真实照明与异动变化，不更新模仿参考");
        check(lamp.SupportUsedDay == state.Day && state.Field.LightSupportUsed && !engine.Execute(support).Success,
            "M2 AC-37：灯具经营日与本次出勤共享唯一支援额度，不能重复领取");

        (state, engine) = Start(false);
        station = state.Node("depot"); station.Built = true; station.BuiltDay = 0; station.Invaded = true; state.Stations[station.Id] = "e6";
        lamp = state.Item("lamp"); lamp.Location = ItemLocation.Installed; lamp.InstalledNodeId = station.Id; lamp.Sealed = true; lamp.ReservedBy = state.Field!.AssignmentId;
        state.Field.AssignedLightSupportId = lamp.Id;
        check(!engine.Execute(support).Success && lamp.SupportUsedDay == 0 && !state.Field.LightSupportUsed,
            "M2 AC-37：网点停业暂停尚未使用的支援，不消耗额度");

        state = WorldFactory.Create(413);
        state.Rule("source-carry").Confirmed = true; state.Rule("source-carry").Archived = true;
        var city = new CityEngine(state); Need(city.AddTask(JobKind.Manual, "source")); Need(city.LockDay());
        engine = new FieldEngine(state); Need(engine.Start(state.LockedPlan.Single()));
        Need(engine.Execute(Move("source-room1")));
        SetHand(state, CardKind.Search, CardKind.Seal);
        Need(engine.Execute(Play(CardKind.Search, 0)));
        state.Item("source-case").Charges = 0;
        var sealSource = new FieldAction { Kind = FieldActionKind.PlayCard, Card = CardKind.Seal, HandSlotIndex = 1, TargetId = "source-box" };
        int actions = state.Field!.Actions;
        check(!engine.Execute(sealSource).Success && state.Field.Actions == actions && state.Item("seal-a").Charges == 2,
            "M2 AC-36：源头专用箱耗尽时，不能以普通隔离工具替代或收费");
        state.Item("source-case").Charges = 1;
        Need(engine.Execute(sealSource));
        check(state.Item("source-box").Sealed && state.Item("source-case").Charges == 0 && state.Item("seal-a").Charges == 2,
            "M2 AC-36：源头现场封装真实消耗专用箱一次，不消耗便携隔离工具");
    }

    private static void CheckContinuation(Action<bool, string> check)
    {
        var (state, engine) = Start(true);
        Need(engine.Execute(Move("hotel-room1"))); Need(engine.Execute(new FieldAction { Kind = FieldActionKind.Search })); Need(engine.EndRound());
        var restored = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(state))!;
        var resumed = new FieldEngine(restored);
        Need(engine.Execute(new FieldAction { Kind = FieldActionKind.Search, AcceptItem = false }));
        Need(resumed.Execute(new FieldAction { Kind = FieldActionKind.Search, AcceptItem = false }));
        check(state.Field!.NormalSearchCounts["hotel-room1"] == restored.Field!.NormalSearchCounts["hotel-room1"] && state.Field.Will == restored.Field.Will && state.Field.Wards == restored.Field.Wards,
            "M2 AC-41：恢复出勤房间计数后，重复正常搜查得到同样 B 规则结果");
        check(state.Item("hotel-clock").Discovered && restored.Item("hotel-clock").Discovered && state.Item("hotel-clock").Location == restored.Item("hotel-clock").Location,
            "M2 AC-41：容器定位与有限库存恢复，重做拒绝入包不复制合同物");
        Need(engine.EndRound()); Need(resumed.EndRound());
        check(state.Field.HandSlots.SequenceEqual(restored.Field.HandSlots) && state.RandomState == restored.RandomState,
            "M2 AC-41：固定槽、抽弃顺序与随机进度按完整检查点一致恢复");
    }

    private static (GameState State, FieldEngine Engine) Start(bool variantB)
    {
        var state = WorldFactory.Create(413, variantB);
        var city = new CityEngine(state);
        Need(city.AddTask(JobKind.Manual, "hotel")); Need(city.LockDay());
        var engine = new FieldEngine(state);
        Need(engine.Start(state.LockedPlan.Single()));
        return (state, engine);
    }

    private static FieldAction Move(string room) => new() { Kind = FieldActionKind.Move, TargetId = room };
    private static FieldAction Play(CardKind card, int slot) => new() { Kind = FieldActionKind.PlayCard, Card = card, HandSlotIndex = slot };
    private static void SetHand(GameState state, params CardKind[] cards)
    {
        state.Field!.Hand = cards.ToList();
        state.Field.HandSlots = cards.Select(c => (CardKind?)c).Concat(Enumerable.Repeat<CardKind?>(null, 5)).Take(5).ToList();
    }
    private static string Section(string value, string start, string end)
    {
        int first = value.IndexOf(start, StringComparison.Ordinal) + start.Length;
        int last = value.IndexOf(end, first, StringComparison.Ordinal);
        return value[first..last];
    }
    private static void Need(ActionResult result)
    {
        if (!result.Success) throw new InvalidOperationException("M2 现场测试前置失败：" + result.Message);
    }
}
