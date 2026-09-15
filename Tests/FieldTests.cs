using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Joy.Core;

namespace Joy.Tests;

public static class FieldTests
{
    public static void Run(Action<bool, string> check)
    {
        var (state, engine) = CreateLegacy();
        check(state.Field!.Actions == 3 && state.Field.Hand.Count == 5 && state.Field.Will == 12 && state.Field.Wards == 1,
            "P-FIELD：3 行动、5 手牌、12 心志、1 护符");
        check(CardCatalog.StartingDeck.Count == 12 && CardCatalog.StartingDeck.Distinct().Count() == 10,
            "P-CARD：起始 12 张、10 种基础牌");
        var before = state.Field.Actions;
        check(!engine.Execute(new FieldAction { Kind = FieldActionKind.Move, TargetId = "vault" }).Success && state.Field.Actions == before,
            "AC-12：无通路基础移动不扣费");
        check(!engine.Execute(new FieldAction { Kind = (FieldActionKind)999 }).Success && state.Field.Actions == before,
            "AC-12：非法动作类型不扣费");
        check(!engine.Execute(new FieldAction { Kind = FieldActionKind.Pickup, TargetId = "missing-item" }).Success && state.Field.Actions == before,
            "AC-12：无效物品引用不扣费");
        check(engine.Preview(new FieldAction { Kind = FieldActionKind.Search }).Text.Contains("入口") && state.Field.RoomId == "entry",
            "AC-21：查看预览不改变真实位置或作用对象");

        TestInventory(check);
        TestMimic(check);
        TestRulePipeline(check);
        TestRoundAndEscape(check);
        TestEvidence(check);
        TestPreviewIsolation(check);
        TestRandomContinuation(check);
    }

    private static void TestInventory(Action<bool, string> check)
    {
        var (state, engine) = CreateLegacy();
        var search = new FieldAction { Kind = FieldActionKind.Search, AcceptItem = false };
        engine.Execute(search);
        check(state.Field!.Actions == 1 && state.Item("entry-item").Discovered && state.Item("entry-item").Location == ItemLocation.Room,
            "AC-12/16：搜查拒绝入包仍支付成本，实物保留原位置");
        state.Field.Capacity = 0;
        var before = state.Field.Actions;
        check(!engine.Execute(new FieldAction { Kind = FieldActionKind.Pickup, TargetId = "entry-item" }).Success && state.Field.Actions == before,
            "AC-12：拾取容量不足不扣费");
        state.Field.Capacity = 6;
        engine.Execute(new FieldAction { Kind = FieldActionKind.Pickup, TargetId = "entry-item" });
        engine.Execute(new FieldAction { Kind = FieldActionKind.Drop, TargetId = "entry-item" });
        engine.EndRound();
        check(!engine.Execute(search).Success && state.Items.Count(i => i.Id == "entry-item") == 1,
            "AC-16：放下不刷新搜查库存，不复制实物");
        engine.Execute(new FieldAction { Kind = FieldActionKind.Pickup, TargetId = "entry-item" });
        state.Item("entry-item").RequiresLightForSealing = true;
        before = state.Field.Actions;
        var seal = new FieldAction { Kind = FieldActionKind.Seal, TargetId = "entry-item" };
        check(!engine.Execute(seal).Success && state.Field.Actions == before && state.Item("kit").Charges == 3,
            "AC-12：公开收容条件不足时不扣行动和工具次数");
        state.Field.Light = true;
        check(engine.Execute(seal).Success && state.Item("entry-item").Sealed && state.Item("kit").Charges == 2,
            "FR-ITEM-04：基础封装按实际工具次数消费并保留身份");

        (state, engine) = CreateLegacy();
        var place = new FieldAction { Kind = FieldActionKind.PlaceRoom, TargetId = "spare" };
        check(engine.Execute(place).Success && state.Field!.Actions == 2 && state.Field.Ink == 1,
            "FR-FLD-04：旅馆放房支付 1 行动和 1 墨水");
        engine.Execute(new FieldAction { Kind = FieldActionKind.Move, TargetId = "spare" });
        check(state.Field!.Ink == 2, "FR-FLD-04：首次进入新房间获得墨水");
        engine.Execute(new FieldAction { Kind = FieldActionKind.Move, TargetId = "entry" });
        engine.EndRound();
        engine.Execute(new FieldAction { Kind = FieldActionKind.Move, TargetId = "spare" });
        check(state.Field.Ink == 2, "AC-12：重返房间不重复获得首次进入墨水");
        state.Node("site").Kind = "Hospital";
        state.Node("site").Rooms.First(r => r.Id == "spare").Placed = false;
        check(!engine.Execute(place).Success, "FR-FLD-04：医院采用固定房网");
    }

    private static void TestMimic(Action<bool, string> check)
    {
        var (state, engine) = CreateLegacy();
        state.Field!.Hand = new() { CardKind.Search, CardKind.Sprint, CardKind.Mimic };
        state.Rules.Add(Rule("dark", RuleTrigger.SearchInDark, 4));
        state.Rules.Add(Rule("move", RuleTrigger.MoveInDark, 1));
        state.Rules.Add(Rule("carry", RuleTrigger.CarryUnsealed, 2));
        state.Field.Light = true;
        engine.Execute(Play(CardKind.Search));
        state.Field.Light = false;
        engine.Execute(Play(CardKind.Sprint, "vault"));
        check(state.Field.RoomId == "vault" && state.Field.MimicReference == CardKind.Search && state.Field.Will == 11,
            "AC-15：疾走逐格移动，同规则每行动最多触发一次，保留搜查参考");
        var result = engine.Execute(Play(CardKind.Mimic));
        check(result.Success && state.Item("vault-item").Location == ItemLocation.Backpack && state.Field.Will == 9,
            "AC-15：模仿领取当前房间实物，豁免搜查标签但保留未封装携带后果");
        check(state.Field.Exhausted.Count(c => c == CardKind.Mimic) == 1 && !state.Field.Discard.Contains(CardKind.Mimic),
            "FR-MIM-04：成功模仿本出勤耗尽");
        state.Field.Hand.Add(CardKind.Mimic);
        state.Field.Actions = 1;
        check(!engine.Execute(Play(CardKind.Mimic)).Success && state.Field.Actions == 1 && state.Field.Hand.Contains(CardKind.Mimic),
            "AC-15：空房模仿不复制旧库存，无效操作不扣费不耗尽");
        check(state.Items.Count == 4, "FR-ITEM-01：普通和模仿操作未产生额外物品实例");

        (state, engine) = CreateLegacy();
        state.Field!.Hand = new() { CardKind.Sprint, CardKind.Mimic };
        engine.Execute(Play(CardKind.Sprint, "hall"));
        check(state.Field.MimicReference is null && !engine.Execute(Play(CardKind.Mimic)).Success,
            "FR-MIM-01：无参考时疾走不创建可模仿指令");

        (state, engine) = CreateLegacy();
        state.Field!.Hand = new() { CardKind.Mimic };
        state.Field.MimicReference = CardKind.Search;
        state.Item("entry-item").Discovered = true;
        check(!engine.Execute(Play(CardKind.Mimic)).Success && state.Field.Actions == 3 && state.Field.Exhausted.Count == 0,
            "AC-15：从未耗尽的模仿也不能搜查空库存，无效操作保留手牌");

        (state, engine) = CreateLegacy();
        state.Field!.Hand = new() { CardKind.Sprint };
        state.Field.MimicReference = CardKind.Search;
        state.Field.Will = 1;
        state.Field.Wards = 0;
        state.Rules.Add(Rule("deadly-step", RuleTrigger.MoveInDark, 1));
        engine.Execute(Play(CardKind.Sprint, "vault"));
        check(state.Field.Finished && state.Field.RoomId == "hall" && state.Field.MimicReference == CardKind.Search,
            "AC-13/15：疾走中途心志归零立即停止后续格移动，参考保持");

        (state, engine) = CreateLegacy();
        state.Field!.Hand = new() { CardKind.Rest, CardKind.Mimic, CardKind.Rest };
        state.Field.Will = 7;
        engine.Execute(Play(CardKind.Rest));
        engine.Execute(Play(CardKind.Mimic));
        check(state.Field.Will == 11 && state.Node("site").Rooms[0].RestUses == 2 && !engine.Execute(Play(CardKind.Rest)).Success,
            "FR-MIM-04：普通和模仿休息共用每个房间实例的 2 次额度");
        engine.EndRound();
        check(state.Field.MimicReference == CardKind.Rest && !state.Field.Hand.Contains(CardKind.Mimic),
            "FR-MIM-01/04：换轮保持参考，耗尽的模仿不进入抽牌");

        (state, engine) = CreateLegacy();
        state.Field!.Hand = new() { CardKind.Search, CardKind.Seal, CardKind.Mimic };
        engine.Execute(Play(CardKind.Search));
        engine.Execute(Play(CardKind.Seal, "entry-item"));
        check(state.Field.MimicReference == CardKind.Seal && !engine.Execute(Play(CardKind.Mimic)).Success && state.Field.Actions == 1,
            "FR-MIM-01：非白名单行动牌更新参考，不回找更早的搜查");
        state.Field.MimicReference = CardKind.Ward;
        state.Field.Wards = 3;
        check(!engine.Execute(Play(CardKind.Mimic)).Success, "FR-MIM-04：模仿护身仍遵守护符上限");
    }

    private static void TestRulePipeline(Action<bool, string> check)
    {
        var (state, engine) = CreateLegacy();
        state.Field!.Wards = 0;
        state.Field.Will = 2;
        var city = Rule("city", RuleTrigger.SearchInDark, 2); city.Scope = RuleScope.City;
        var local = Rule("local", RuleTrigger.SearchInDark, -5); local.Scope = RuleScope.Local;
        state.Rules.Add(local); state.Rules.Add(city);
        engine.Execute(new FieldAction { Kind = FieldActionKind.Search });
        check(state.Field.Finished && state.Field.Will == 0 && !local.Observed,
            "AC-13：全城先于地方；心志归零后不执行后续恢复");
        check(state.Item("entry-item").Location == ItemLocation.Room && state.Item("entry-item").Discovered && state.Item("kit").Location == ItemLocation.Room,
            "AC-11/40：失败实物和带入工具遗落且保持唯一，可重新出勤取回");

        (state, engine) = CreateLegacy();
        state.Field!.Disturbance = 7;
        state.Field.Will = 3;
        state.Field.Wards = 3;
        var threshold = Rule("threshold", RuleTrigger.SearchInDark, 0); threshold.DisturbanceGain = 1;
        state.Rules.Add(threshold);
        engine.Execute(new FieldAction { Kind = FieldActionKind.Search });
        check(state.Field.Finished && state.Field.Will == 0 && state.Field.Disturbance == 4 && state.Field.Wards == 3,
            "AC-14：行动中异动达到阈值立即袭击，护符无法抵扣");

        (state, engine) = CreateLegacy();
        state.Field!.Wards = 2;
        var ward1 = Rule("a", RuleTrigger.SearchInDark, 2); ward1.Wardable = true;
        var ward2 = Rule("b", RuleTrigger.SearchInDark, 2); ward2.Wardable = true;
        state.Rules.Add(ward1); state.Rules.Add(ward2);
        engine.Execute(new FieldAction { Kind = FieldActionKind.Search });
        check(state.Field.Wards == 0 && state.Field.Will == 12, "AC-14：多个可抵挡规则逐项消耗护符");
    }

    private static void TestRoundAndEscape(Action<bool, string> check)
    {
        var (state, engine) = CreateLegacy();
        state.Field!.Round = 12;
        state.Field.Actions = 0;
        check(engine.Withdraw().Success && state.Field.Escaped && state.Field.Actions == 0,
            "AC-11：最后一轮入口可在零行动时免费撤离");
        (state, engine) = CreateLegacy();
        state.Field!.Round = 12;
        engine.EndRound();
        check(state.Field.Finished && !state.Field.Escaped && state.Field.EndReason.Contains("封门"),
            "AC-11：最后一轮继续结束，即使在入口仍封门失败");

        (state, engine) = CreateLegacy();
        state.Field!.Light = true;
        state.Field.Will = 2;
        state.Field.Wards = 0;
        state.Field.Shadows.Add(new ShadowState { RoomId = "entry", AnnouncedRound = 0, Damage = 2 });
        state.Rules.Add(Rule("healing-light", RuleTrigger.LightEnds, -6));
        check(engine.PreviewEndRound().Text.Contains("不再执行后续效果"), "AC-27：轮末预览标明致死后不执行补救");
        engine.EndRound();
        check(state.Field.Finished && state.Field.Light && state.Field.Disturbance == 0,
            "AC-27：敌影致死后灯光、异动和正面效果全部停止");

        (state, engine) = CreateLegacy();
        state.Field!.Wards = 0;
        state.Field.Light = true;
        state.Field.Disturbance = 7;
        state.Field.Shadows.Add(new ShadowState { RoomId = "entry", AnnouncedRound = 0, Damage = 2 });
        var light = Rule("light", RuleTrigger.LightEnds, 1); light.Confirmed = true;
        state.Rules.Add(light);
        check(engine.PreviewEndRound().Text.Contains("预计轮末心志 6"), "AC-27：全部条件已知时，轮末预览按真实顺序计算");
        engine.EndRound();
        check(state.Field.Will == 6 && !state.Field.Light && state.Field.Disturbance == 4,
            "AC-14：敌影 → 灯光 → 异动阈值按序落实");

        (state, engine) = CreateLegacy();
        state.Field!.Wards = 0;
        state.Field.Shadows.Add(new ShadowState { RoomId = "entry", AnnouncedRound = 1, Damage = 2 });
        engine.EndRound();
        check(state.Field.Will == 12, "FR-FLD-09：敌影出现当轮只预告");
        engine.EndRound();
        check(state.Field.Will == 10, "FR-FLD-09：敌影最早下一轮末攻击");
    }

    private static void TestEvidence(Action<bool, string> check)
    {
        var (state, engine) = CreateLegacy();
        var rule = Rule("evidence", RuleTrigger.Knock, 2);
        state.Rules.Add(rule);
        check(!engine.Confirm(rule.Id, 1).Success, "FR-RULE-03：没有证据或观察不能猜测确认");
        state.Field!.Hand = new() { CardKind.Investigate };
        engine.Execute(Play(CardKind.Investigate));
        check(rule.EvidenceFound && !rule.Confirmed && !rule.Archived, "FR-DAY-05：调查只取得现场可靠线索，不自动确认或归档");
        engine.Confirm(rule.Id, 0);
        check(rule.WrongCandidates.Contains(0) && state.Field.Disturbance == 2 && !engine.Confirm(rule.Id, 1).Success,
            "FR-RULE-04：错误候选保留，当前现场每轮仅一次确认");
        engine.EndRound();
        check(!engine.Confirm(rule.Id, 0).Success, "FR-RULE-04：跨轮也不能重复提交同一错误候选");
        engine.Confirm(rule.Id, 1);
        check(rule.Confirmed && rule.ConfirmationRewardClaimed && state.Field.Coins == 1 && !engine.Confirm(rule.Id, 1).Success,
            "AC-10：正确确认只发一次现场奖励，重复确认被拒绝");

        (state, engine) = CreateLegacy();
        rule = Rule("rare", RuleTrigger.Rest, 1);
        state.Rules.Add(rule);
        state.Field!.RevealCharges = 1;
        var reveal = new FieldAction { Kind = FieldActionKind.Reveal, TargetId = "missing" };
        check(!engine.Execute(reveal).Success && state.Field.RevealCharges == 1 && state.Field.Actions == 3,
            "AC-32：稀有揭晓无目标不消耗机会或行动");
        reveal.TargetId = rule.Id;
        engine.Execute(reveal);
        check(rule.Confirmed && rule.ConfirmationRewardClaimed && rule.RevealedBy.Length > 0 && state.Field.Coins == 0 && state.Field.RevealCharges == 0,
            "AC-32：稀有来源只揭晓选定实例，明确不发普通确认奖励");
        check(!engine.Confirm(rule.Id, 1).Success, "AC-32：揭晓与普通确认共用唯一台账");
    }

    private static void TestPreviewIsolation(Action<bool, string> check)
    {
        var (first, firstEngine) = CreateLegacy();
        var (second, secondEngine) = CreateLegacy();
        var a = Rule("hidden", RuleTrigger.SearchInDark, 2);
        var b = Rule("hidden", RuleTrigger.Rest, 9);
        b.CorrectCandidate = 2; b.Answer = "隐藏的另一个答案";
        first.Rules.Add(a); second.Rules.Add(b);
        var search = new FieldAction { Kind = FieldActionKind.Search };
        var firstPreview = firstEngine.Preview(search);
        var secondPreview = secondEngine.Preview(search);
        check(firstPreview == secondPreview, "AC-26：不同隐藏守则不改变动作预览、费用或启用状态");
        check(firstEngine.PreviewEndRound() == secondEngine.PreviewEndRound(), "AC-26/27：未知规则不泄漏轮末秘密数值");
        check(!firstPreview.Text.Contains(a.Answer) && !firstPreview.Text.Contains(a.Clue),
            "AC-26：未取得线索与隐藏答案不提前出现在预览");
    }

    private static void TestRandomContinuation(Action<bool, string> check)
    {
        var (state, engine) = CreateLegacy();
        var restored = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(state))!;
        var restoredEngine = new FieldEngine(restored);
        engine.EndRound(); restoredEngine.EndRound();
        check(state.Field!.Hand.SequenceEqual(restored.Field!.Hand) && state.RandomState == restored.RandomState,
            "AC-18：存档恢复手牌、牌库及随机进度，同操作不重抽");
        state.Field.MimicReference = CardKind.Search;
        state.Field.Exhausted.Add(CardKind.Mimic);
        state.Node("site").Rooms[0].RestUses = 2;
        restored = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(state))!;
        check(restored.Field!.MimicReference == CardKind.Search && restored.Field.Exhausted.Contains(CardKind.Mimic) && restored.Node("site").Rooms[0].RestUses == 2,
            "FR-SAVE-02：模仿参考、耗尽和房间实例额度可完整恢复");
    }

    private static FieldAction Play(CardKind kind, string target = "") => new()
        { Kind = FieldActionKind.PlayCard, Card = kind, TargetId = target };

    private static RuleState Rule(string id, RuleTrigger trigger, int damage) => new()
    {
        Id = id, NodeId = "site", Scope = RuleScope.Local, Trigger = trigger, WillDamage = damage, Wardable = false,
        CorrectCandidate = 1, Candidates = new() { "错误候选一", "正确候选", "错误候选二" },
        Clue = "这是一条尚未取得的可靠线索", Answer = "这是尚未确认的完整规则"
    };

    private static (GameState, FieldEngine) CreateLegacy()
    {
        var state = new GameState
        {
            Scenario = "Legacy",
            RandomState = 314159,
            Nodes = new()
            {
                new CityNode
                {
                    Id = "site", Name = "测试旅馆", Kind = "Hotel",
                    Rooms = new()
                    {
                        new RoomState { Id = "entry", Name = "入口", IsEntrance = true, Neighbors = new() { "hall", "spare" }, AllowsKnock = true },
                        new RoomState { Id = "hall", Name = "走廊", Neighbors = new() { "entry", "vault" } },
                        new RoomState { Id = "vault", Name = "镜厅", Neighbors = new() { "hall" } },
                        new RoomState { Id = "spare", Name = "空白房", Placed = false, Neighbors = new() { "entry" } }
                    }
                }
            },
            Items = new()
            {
                new ItemState { Id = "kit", Name = "封装工具", Kind = "SealKit", Location = ItemLocation.Warehouse, Charges = 3, Size = 0 },
                new ItemState { Id = "entry-item", Name = "入口物品", NodeId = "site", RoomId = "entry", Required = true },
                new ItemState { Id = "hall-item", Name = "走廊物品", NodeId = "site", RoomId = "hall" },
                new ItemState { Id = "vault-item", Name = "未封装镜子", NodeId = "site", RoomId = "vault", RequiresSealing = true }
            }
        };
        var engine = new FieldEngine(state);
        var started = engine.Start(new Assignment { Id = "manual", Kind = JobKind.Manual, NodeId = "site", ToolIds = new() { "kit" } });
        if (!started.Success) throw new InvalidOperationException(started.Message);
        return (state, engine);
    }
}
